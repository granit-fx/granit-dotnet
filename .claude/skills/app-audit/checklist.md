# Granit Integration Audit — Checklist

Exhaustive, copy-pasteable checks for auditing a **consuming application** (a host
built on the Granit NuGet packages) — not the framework itself. Each item gives the
**ripgrep query**, the **pass criterion**, the **severity** of a miss, and **why** it
matters. Run from the target repo root. `$ROOT` = repo root.

Two reference hosts to calibrate against:

- **Modular monolith** — `granit-showcase-dotnet`: one `Showcase.Host/Program.cs`
  (composition root, ~800 lines of `Map*`), `ShowcaseHostModule` (`[DependsOn]` graph +
  service config), `ShowcaseInfrastructureModule` (all `AddGranit*EntityFrameworkCore`),
  two DbContexts (`ShowcaseHostDbContext` host-schema + `ShowcaseTenantDbContext`
  schema-per-tenant), modules under `src/Modules/`.
- **Microservices** — `granit-microservice-template`: one `Program.cs` +
  `{Svc}ServiceModule` + `{Svc}DbContext` + `Persistence/Migrations/` per service,
  cross-cutting concerns in `GranitMicroservice.Shared.Hosting` (`SharedHostingModule`
  `[DependsOn]` graph + `AddSharedHostingAsync`).

---

## 0. Inventory & shape detection

| # | Check | Command | Pass |
| - | ----- | ------- | ---- |
| 0.1 | Repo is a Granit consumer | `rg -l "Granit" "$ROOT/Directory.Packages.props"` | Granit packages present in CPM |
| 0.2 | Shape | `rg -l "AddGranitAsync" "$ROOT"/src/**/Program.cs` | 1 root ⇒ monolith; ≥2 ⇒ microservices |
| 0.3 | Package inventory | `rg -o 'PackageVersion Include="(Granit[^"]*)"' -r '$1' "$ROOT/Directory.Packages.props" \| sort -u` | the master list to drive the matrix |
| 0.4 | Actually-referenced packages | `rg -o 'PackageReference Include="(Granit[^"]*)"' -r '$1' "$ROOT"/src/**/*.csproj \| sort -u` | only these are live (vs CPM-declared-but-unused) |
| 0.5 | Composition roots | `rg -l "\[DependsOn" "$ROOT"/src --type cs` | the `*Module.cs` files; plus each `Program.cs` |

> CPM-declared but never `<PackageReference>`d is **LOW** (hygiene); referenced but never
> wired is **HIGH** (see §1). Resolve transitive refs: a project's effective package set
> includes packages of every `<ProjectReference>` it pulls (esp. `*.Shared.Hosting`).

---

## 1. Package → wiring matrix  (scope `wiring`, the core pass)

For each module family, confirm each referenced layer has its companion call. Build the
table family-by-family. Helper greps (run over `Program.cs` + `*Module.cs` +
`*Extensions.cs`):

```bash
# All Granit service registrations actually called
rg -o 'AddGranit[A-Za-z0-9]+' "$ROOT"/src --type cs | sort -u
# All Granit endpoint maps actually called
rg -o 'MapGranit[A-Za-z0-9]+' "$ROOT"/src --type cs | sort -u
# All Granit middleware actually used
rg -o 'UseGranit[A-Za-z0-9]+' "$ROOT"/src --type cs | sort -u
# Full DependsOn graph
rg -o 'DependsOn\(typeof\((Granit[A-Za-z0-9]+Module)\)\)' -r '$1' "$ROOT"/src --type cs | sort -u
```

| # | Check | Pass criterion | Sev |
| - | ----- | -------------- | --- |
| 1.1 | **Base module** `Granit.{M}` referenced ⇒ activated | `[DependsOn(typeof(Granit{M}Module))]` present **or** explicit `AddGranit{M}(`. Account for zero-`Module` packages (`Granit.MultiTenancy`, `Granit.Guids`, `Granit.Timing`) — soft API use is enough | HIGH |
| 1.2 | **`.Endpoints` referenced ⇒ mapped** | a `MapGranit{Verb}(` call exists for it **and** `[DependsOn(typeof(Granit{M}EndpointsModule))]`. Verify the actual map name from the package's `*EndpointRouteBuilderExtensions` (not all are `MapGranit{M}`: `MapGranitConversations`, `MapGranitPrompts`, `MapGranitUserSessions`, `MapGranitAccount`, `MapGranitBff`, `MapGranitODataEndpoints`, …) | HIGH |
| 1.3 | **`.EntityFrameworkCore` referenced ⇒ registered** | `AddGranit{M}EntityFrameworkCore(` present (shared and, for tenant-isolated contexts under SchemaPerTenant, `configureSchemaPerTenant` too) | HIGH |
| 1.4 | **EF module ⇒ migrated** | tables covered: either an isolated DbContext with its own `Migrations/`, **or** folded via `Configure{M}Module()` in a host/tenant DbContext whose `Migrations/` exist | HIGH |
| 1.5 | **`.{Provider}` referenced ⇒ provider wired** | the active provider has `AddGranit{M}{Provider}()` / `builder.AddGranit{Provider}()` / `[DependsOn(...Module)]`. Examples: `BlobStorage.S3`→`AddGranitBlobStorageS3`, `Vault.HashiCorp`→`[DependsOn(GranitVaultHashiCorpModule)]`+health check, `IpGeolocation.MaxMind`→`builder.AddGranitIpGeolocationMaxMind`, `Notifications.Brevo`→`AddGranitNotificationsBrevo`, `AI.Ollama`→`AddGranitAIOllama` | HIGH |
| 1.6 | **Abstraction has exactly one+ active provider** | a base needing a provider (BlobStorage, Vault, IpGeolocation, LanguageDetection, TextExtraction, Browsing, AI) must have at least one concrete provider wired; multiple referenced-but-only-one-active is fine if documented | HIGH |
| 1.7 | **`.Notifications` referenced ⇒ reachable** | `[DependsOn(typeof(Granit{M}NotificationsModule))]` | MED |
| 1.8 | **`.BackgroundJobs` referenced ⇒ store wired** | `[DependsOn(...BackgroundJobsModule)]` + `AddGranitBackgroundJobsEntityFrameworkCore(` present | MED |
| 1.9 | **Orphan package** | every referenced `Granit.*` matched at least one wiring rule above | HIGH |
| 1.10 | **Dangling wiring** | every `MapGranit*`/`AddGranit*EntityFrameworkCore` call has a backing package reference | LOW |

> **The user's headline ask maps to 1.2 + 1.3 + 1.4**: *"are the endpoints present, are
> the EF Core modules present when the module is present"*. Lead the report with this.

### Avoiding false positives — alternative wiring mechanisms

Several modules do **not** expose an `AddGranit{M}EntityFrameworkCore` and wire their
tables through another path. Before flagging an EFC package as "not registered", confirm
the tables aren't landing via one of these (all observed in `granit-showcase-dotnet`):

- **Folded into the base `Add`** — e.g. `Granit.OpenIddict.EntityFrameworkCore` is wired
  by `builder.AddGranitOpenIddict(o => o.UseNpgsql(...))`, not a separate EFC call.
- **Sub-builder** — `Granit.Privacy.EntityFrameworkCore` via
  `privacyBuilder.UseEntityFrameworkCoreTrackers<TDb>()`; `Granit.Parties.Deduplication.*`
  via `AddGranitPartiesDeduplication()`.
- **`[DependsOn]` self-registration** — `Granit.Encryption.EntityFrameworkCore` registers
  its services from its own module `ConfigureServices`; no host call.
- **Endpoint auto-map vs explicit map** — most `.Endpoints` need an explicit `MapGranit*`,
  but a few modules auto-map in `OnApplicationInitialization`. Decide by checking the
  package's public `*EndpointRouteBuilderExtensions`: **if it exposes a `MapGranit*`
  method, an explicit call is required** (the showcase's `Granit.Entities.Customization.Endpoints`
  ships `MapGranitEntitiesCustomization(...)` and not calling it leaves the routes
  unmounted — a real HIGH). Verify the method via the restored NuGet XML doc:
  `rg 'name="M:[^"]*Map[^"]*"' ~/.nuget/packages/<pkg>/<ver>/lib/*/*.xml`.

> **ripgrep gotcha:** to strip filenames use `--no-filename` (or `-I`), **never `-h`** —
> `-h` is ripgrep's `--help` and dumps the man page into your analysis.

More false-positive guards (all hit on `granit-microservice-template`):

- **Scan the whole service, not just `Program.cs`.** Channel/EFC/provider registration
  frequently lives in the service's `*ServiceModule.ConfigureServices` (e.g.
  `AddGranitNotificationsEmail` / `AddGranitNotificationsEntityFrameworkCore` are in
  `NotificationServiceModule.cs`, not `Program.cs`). Grep `src/<svc>/**/*.cs`.
- **Read the match's context — comments lie.** A raw hit on
  `AddGranitAuditingEntityFrameworkCore` may be inside a `//` comment explaining why it is
  *deliberately not* called (the gateway stubs `IAuditingCleaner` with a `NullAuditingCleaner`
  because `GranitBffEndpointsModule` transitively pulls `GranitAuditingModule`). Confirm the
  match is executable code before flagging a dangling call.
- **CPM version pins for transitive deps are not orphans.** `Granit.Events`,
  `Granit.Wolverine`, `Granit.Vault`, `Granit.Diagnostics` may appear in
  `Directory.Packages.props` with zero `<PackageReference>` — they back transitive deps
  (`Granit.Events.Wolverine` → `Granit.Events`, etc.). Only flag a CPM entry as orphan if
  nothing references it **transitively** either.
- **One migration can cover many EF stores.** A service that calls three
  `AddGranit*EntityFrameworkCore` may still ship a single migration when all stores fold
  into one `GranitDbContext` (IdentityService folds Identity + Federated + Auditing into
  `IdentityServiceDbContext` via `ConfigureIdentityModule()` / `ConfigureGranitIdentityModule()`
  / `ConfigureAuditingModule()`). Verify coverage by the **table list in the model snapshot**
  (`rg 'ToTable\("([a-z_]+)"' *ModelSnapshot.cs`), not by counting migration files.
- **`UseAuthentication`/`UseAuthorization` are auto-added.** `WebApplication` inserts both
  before endpoints when auth services are registered — their absence from `Program.cs` is
  not a finding (the JwtBearer stack comes via `Shared.Hosting`).

---

## 2. Endpoints  (scope `endpoints`)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 2.1 | Every `.Endpoints` mapped | per §1.2 | a `MapGranit*` per referenced endpoints package | HIGH |
| 2.2 | Versioned group present | `rg -n "MapGroup\(.*v\{version" "$ROOT"/src --type cs` | API surface mounted under a versioned group (`api/v{version:apiVersion}`) where the host uses `Granit.Http.ApiVersioning` | MED |
| 2.3 | App-owned endpoints follow conventions | inspect `src/**/Endpoints/*.cs` | named `private static` handlers (no inline lambdas), `.WithName/.WithSummary/.WithDescription/.Produces*/.ProducesProblem`, `.WithTags("Title Case")` | MED |
| 2.4 | Auth on protected groups | `rg -n "RequireAuthorization\|RequireGranitRateLimiting" "$ROOT"/src --type cs` | mutating/PII endpoints carry `.RequireAuthorization()` | HIGH |
| 2.5 | App `*Request` has a validator | `rg -l "GranitValidator<" "$ROOT"/src --type cs` | each `*Request` in an endpoints layer has a `GranitValidator<T>` | MED |

---

## 3. EF Core & persistence  (scope `efcore` / `persistence`)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 3.1 | EF module registered | per §1.3 | one `AddGranit{M}EntityFrameworkCore` per referenced EFC package | HIGH |
| 3.2 | DbContext base | `rg -n "class .*DbContext" "$ROOT"/src --type cs` | owns `IMultiTenant` ⇒ inherits `GranitDbContext(options, currentTenant, dataFilter)`; otherwise `DbContext`/`GranitDbContext` with a deliberate comment | HIGH |
| 3.3 | Override hook | `rg -n "OnModelCreating\|OnGranitModelCreating" "$ROOT"/src --type cs` | `GranitDbContext` derivatives override `OnGranitModelCreating` (never `OnModelCreating`) | HIGH |
| 3.4 | Registration helper | `rg -n "AddGranitDbContext\|AddGranitIsolatedDbContext\|AddDbContext\b\|AddDbContextFactory" "$ROOT"/src --type cs` | Granit DbContexts use `AddGranitDbContext`/`AddGranitIsolatedDbContext` (Scoped); **bare `AddDbContext`/`AddDbContextFactory` for a Granit DbContext = FAIL** (drops interceptors + parameterised tenant filter) | HIGH |
| 3.5 | No unnamed query filters | `rg -n "HasQueryFilter\(" "$ROOT"/src --type cs` | none unnamed in app DbContexts (handled by `ApplyGranitConventions`) | MED |
| 3.6 | Folding consistency | `rg -n "Configure[A-Za-z]+Module\(" "$ROOT"/src --type cs` | each folded module's `Configure{M}Module()` call exists in the owning DbContext; its `*DbProperties.DbSchema` pin matches (host vs null/tenant) | MED |
| 3.7 | Migrations exist & current | `fd -t d Migrations "$ROOT"/src` | a `Migrations/` per owned DbContext with `*_Init` + `*ModelSnapshot.cs` | HIGH |
| 3.8 | Provider = Npgsql | `rg -n "UseNpgsql\|UseSqlServer\|UseSqlite" "$ROOT"/src --type cs` | PostgreSQL/Npgsql default; `UseVector()` present wherever `Granit.Indexing*` / a `vector` column is folded | MED |
| 3.9 | Microservice DB isolation | `rg -n "GetConnectionString\(" "$ROOT"/src --type cs` | each service uses its own connection-string name; no shared DbContext type or connection across services | HIGH |
| 3.10 | Design-time factory (microservices) | `fd "DbContextFactory.cs" "$ROOT"/src` | `IDesignTimeDbContextFactory` present where `dotnet ef` is used for migrations | LOW |

---

## 4. Host bootstrap  (scope `host`)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 4.1 | Granit bootstrap | `rg -n "AddGranitAsync" "$ROOT"/src --type cs` | `await builder.AddGranitAsync<TRoot>()` (monolith) or per-service `AddGranitAsync(g => g.AddModule<…>())` | HIGH |
| 4.2 | Init hook | `rg -n "UseGranitAsync" "$ROOT"/src --type cs` | `await app.UseGranitAsync()` present, **before** any `MapGranit*` | HIGH |
| 4.3 | Migrate flag path | `rg -n "HasGranitMigrateFlag\|RunGranitMigrationsAsync\|AddGranitMigrateSupport" "$ROOT"/src --type cs` | `--migrate` handled and returns before the normal pipeline; `AddGranitMigrateSupport` wired | MED |
| 4.4 | Health checks per dependency | `rg -n "AddGranit.*HealthCheck\|AddRabbitMqHealthCheck\|MapGranitHealthChecks" "$ROOT"/src --type cs` | `AddGranitDbContextHealthCheck<TDb>()` per owned DbContext + Redis/Keycloak/Vault/RabbitMq matching referenced infra; `MapGranitHealthChecks()` mounted | MED |
| 4.5 | OpenAPI native only | `rg -ni "swashbuckle\|nswag\|AddSwaggerGen\|UseSwagger" "$ROOT"/src "$ROOT"/Directory.Packages.props` | **zero hits** — Swashbuckle/NSwag banned; use `UseGranitApiDocumentation` + native `AddOpenApi` | HIGH |
| 4.6 | API documentation mounted | `rg -n "UseGranitApiDocumentation" "$ROOT"/src --type cs` | present | MED |
| 4.7 | ServiceDefaults (Aspire) | `rg -n "AddServiceDefaults" "$ROOT"/src --type cs` | each Aspire service calls `AddServiceDefaults()` | LOW |

---

## 5. Middleware order  (scope `middleware`)

Extract the `Use*` sequence and assert relative ordering. Command:

```bash
rg -n "app\.Use[A-Za-z]+\(|app\.MapGranit" "$ROOT"/src/**/Program.cs
```

| # | Rule | Pass | Sev |
| - | ---- | ---- | --- |
| 5.1 | Exception handling first | `UseGranitExceptionHandling` before functional middleware | HIGH |
| 5.2 | Security headers early | `UseGranitSecurityHeaders` before endpoint exec | MED |
| 5.3 | Authn before authz | `UseAuthentication` precedes `UseAuthorization` | HIGH |
| 5.4 | Tenancy before cache | `UseGranitMultiTenancy` **precedes** `UseGranitOutputCaching` — **else cross-tenant cache disclosure** | HIGH |
| 5.5 | Rate limiting after authn+tenancy | `UseGranitRateLimiting` after `UseAuthentication`/`UseGranitMultiTenancy` (partitions on principal/tenant) | MED |
| 5.6 | BFF injection placement | `UseGranitBffTokenInjection` after rewrite, before `UseAuthentication` (BFF hosts only) | MED |
| 5.7 | DPoP after authn | `UseGranitDPoPValidation` after `UseAuthentication`, before `UseAuthorization` (DPoP hosts only) | MED |

---

## 6. Auth  (scope `auth`)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 6.1 | JWT/Keycloak wired | `rg -n "AddGranitAuthenticationJwtBearer\|JwtBearerKeycloak\|UseAuthentication" "$ROOT"/src --type cs` | referenced auth package ⇒ scheme + `UseAuthentication` | HIGH |
| 6.2 | OpenIddict complete | `rg -n "AddGranitOpenIddict\|MapGranitOpenIddictServer\|AddGranitOpenIddictEntityFrameworkCore" "$ROOT"/src --type cs` | server + EF stores + protocol endpoints all present together | HIGH |
| 6.3 | BFF complete | `rg -n "MapGranitBff\|UseGranitBffTokenInjection\|AddGranitBffEntityFrameworkCore" "$ROOT"/src --type cs` | endpoints + token injection + (session store if EF BFF) | MED |
| 6.4 | **No `RequireRole`** | `rg -n "RequireRole\(" "$ROOT"/src "$ROOT"/tests --type cs` | **zero hits** (incl. tests) — permission-based only | HIGH |
| 6.5 | Authorization policies | `rg -n "AddAuthorizationBuilder\|AddAuthorization\(" "$ROOT"/src --type cs` | `AddAuthorizationBuilder()` (not `AddAuthorization(Action<>)`) | LOW |

---

## 7. Configuration  (scope `config`)

For each referenced module, confirm its appsettings section exists. Command per key:

```bash
rg -n '"ConnectionStrings"|"Http"|"RateLimiting"|"Wolverine"|"MultiTenancy"|"OpenIddict"|"Notifications"|"BlobStorage"|"Vault"|"Cache"|"IpGeolocation"|"Authentication"|"Bff"' "$ROOT"/src/**/appsettings*.json
```

| # | Check | Pass | Sev |
| - | ----- | ---- | --- |
| 7.1 | Connection strings | every owned DbContext's connection name exists in config/Aspire | HIGH |
| 7.2 | Module sections present | each referenced module that requires config has a bound section (RateLimiting policies, Http:ApiDocumentation, Http:Cors, Notifications channels, OpenIddict:Seeding, MultiTenancy:TenantIsolation, Vault, Cache, IpGeolocation:MaxMind, …) | MED |
| 7.3 | **No hardcoded secrets** | `rg -ni "password\s*=\|secret\s*=\|apikey\s*=\|\"sk-\|client_secret" "$ROOT"/src "$ROOT"/src/**/appsettings*.json` ⇒ scrutinise; production secrets via Vault/ExternalSecret | HIGH |
| 7.4 | No PII in logs | spot-check `[LoggerMessage]`/log calls for emails, IPs, names | HIGH |
| 7.5 | Per-environment overrides | dev-only relaxations (`appsettings.Development.json`) don't leak to prod (CORS `*`, COOP `unsafe-none`, etc.) | MED |

---

## 8. Localization  (scope `localization`)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 8.1 | Languages configured | `rg -n "GranitLocalizationOptions\|LanguageInfo\(" "$ROOT"/src --type cs` | `options.Languages` populated with a default | MED |
| 8.2 | Endpoints mounted | `rg -n "MapGranitLocalization" "$ROOT"/src --type cs` | present when `Granit.Localization.Endpoints` referenced | MED |
| 8.3 | App resource culture parity | `fd -e json . "$ROOT"/src -p "Localization"` | app-owned `Localization/**/*.json` exists for all 18 cultures (15 base + 3 regional); regional files hold only diffs | MED |

---

## 9. Providers & notifications  (scope `providers` / `notifications`)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 9.1 | Storage provider | `rg -n "AddGranitBlobStorageS3\|AddGranitBlobStorageAzure" "$ROOT"/src --type cs` | a concrete blob provider when `Granit.BlobStorage*` used | HIGH |
| 9.2 | Secrets provider | `rg -n "GranitVaultHashiCorp\|AddGranitVault" "$ROOT"/src --type cs` | Vault provider when `Granit.Vault*` used (dev may no-op) | HIGH |
| 9.3 | Notification channels | `rg -n "AddGranitNotifications(Email\|Sms\|Sse\|MobilePush\|Brevo\|EmailSmtp)" "$ROOT"/src --type cs` | ≥1 delivery channel + `AddGranitNotificationsEntityFrameworkCore` when notifications used | MED |
| 9.4 | App notification templates | `fd -e html . "$ROOT"/src -p "Templates"` | app-owned notification templates have EN+FR (+ AUTO-TRANSLATED others), `<title>` first line, `*Display` for list fields | MED |
| 9.5 | AI provider | `rg -n "AddGranitAIOllama\|AddGranitAIOpenAI\|AddGranitAIAnthropic" "$ROOT"/src --type cs` | a chat/completion provider when `Granit.AI*` used | MED |
| 9.6 | Imaging/extraction native deps | inspect `[DependsOn]` for `*.MagickNet`/`*.PuppeteerSharp`/`*.Office` | native runtime deps (ImageMagick, Chromium, LibreOffice) documented for prod images | LOW |

---

## 10. Build & tests (close-out)

| # | Check | Command | Pass | Sev |
| - | ----- | ------- | ---- | --- |
| 10.1 | Solution builds | `dotnet build "$ROOT"/*.slnx` (or affected project after `--fix`) | clean | HIGH |
| 10.2 | Tests pass | `dotnet test "$ROOT"/*.slnx` (or per project) | green | HIGH |
| 10.3 | Format clean | `dotnet format "$ROOT"/*.slnx --verify-no-changes` | no diff | MED |
| 10.4 | Integration tests per service | `fd "Tests.Integration" "$ROOT"/tests -t d` | each service/host has an integration test project exercising real endpoints (`GranitEndpointTestHost`) | MED |

> Build/test only the affected project or `.slnf` shard — never the full solution
> (Roslyn OOMs). For the showcase/template `.slnx` is acceptable as they are small.

---

## Quick severity legend

- **HIGH** — silent no-op or runtime failure or security bug (referenced-but-unwired,
  endpoints not mounted, EF not registered/migrated, missing provider, bad middleware
  order, `RequireRole`, Swashbuckle, shared microservice DB, hardcoded secret).
- **MED** — degraded behaviour (no notification channel, no job store, missing health
  check, bare `AddDbContext`, missing config/culture).
- **LOW** — hygiene (orphan `using`/CPM entry, satellite without `[DependsOn]`, stale
  snapshot, missing design-time factory).
