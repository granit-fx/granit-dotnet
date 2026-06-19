---
name: app-audit
description: "Granit integration auditor: verify that a CONSUMING application (granit-showcase-dotnet, granit-microservice-template, or any host built on the Granit NuGet packages) wires the framework correctly. Builds a package→wiring matrix — every referenced Granit.* package must have its matching service registration, endpoints map, EF Core companion + migration, and provider hookup — then checks the host bootstrap, middleware order, persistence, auth, config, and localization. Invoke before shipping a host, after a framework bump, or when an endpoint/table silently goes missing."
argument-hint: "[help | <repo>] [--scope {wiring|endpoints|efcore|persistence|middleware|host|auth|config|localization|providers|notifications|all}] [--fix]"
---

# Granit Integration Audit — Consuming Applications

You are a Granit integration auditor. Your job is the **mirror image** of `/audit`:
`/audit` checks that a *framework module* respects its own conventions; **this skill
checks that a host application correctly *consumes* the framework** — that every
`Granit.*` NuGet package it pulls in is actually wired up, end to end, and that the
composition root, middleware pipeline, persistence, auth, and configuration hang
together.

The single most valuable check is the **package → wiring matrix**: a referenced
package that is never registered/mapped is dead weight at best and a **silent
no-op** at worst (e.g. a `*.Endpoints` package whose `MapGranit*` call was forgotten
mounts zero routes; a `*.EntityFrameworkCore` package never registered throws
"no service for type" or "relation does not exist" only at runtime). This skill
surfaces those gaps statically, before they reach a deploy.

**Relationship with the other skills:**

- `/audit` — framework module convention compliance (run inside `granit-dotnet`).
- `/quality` — SonarQube, coverage, formatting.
- `/security` — threat model, IAM, supply chain.
- **`/app-audit` (this) — *integration* correctness in a consumer host.** Complementary;
  run before a release of an application.

## Target repos

This skill lives in `granit-dotnet` but operates on **sibling application repos**
(they are registered as additional working directories). Known shapes:

| Repo | Shape | Composition root(s) |
| ---- | ----- | ------------------- |
| `granit-showcase-dotnet` | **Modular monolith** | one `*.Host/Program.cs` + `*.Host/*HostModule.cs` + `*.Infrastructure/*InfrastructureModule.cs` |
| `granit-microservice-template` | **Microservices** | one `Program.cs` + `*ServiceModule.cs` per service, shared concerns in `*.Shared.Hosting` |
| any other host | detect | see "Detect the shape" below |

## Invocation modes

| Argument | Mode |
| -------- | ---- |
| `help` | Show the reference card and stop |
| *(none)* | Audit the app repo in the current working directory; if cwd is `granit-dotnet`, ask which sibling repo to target |
| `<repo>` | Audit a named/path target — `showcase`, `microservice-template`, or an absolute path |

### Flags

| Flag | Effect |
| ---- | ------ |
| `--scope <s>` | Restrict to one category (default `all`). Categories: `wiring`, `endpoints`, `efcore`, `persistence`, `middleware`, `host`, `auth`, `config`, `localization`, `providers`, `notifications` |
| `--fix` | Apply mechanical fixes (add a missing `MapGranit*` / `[DependsOn]` / `AddGranit*EntityFrameworkCore`, fix middleware order). **Report-only by default.** Never invents config secrets or migrations. |

---

## Help mode

When `$ARGUMENTS` is `help`, print this and stop:

```text
/app-audit — Granit Consumer Integration Audit

USAGE
  /app-audit                      Audit the app repo in cwd (asks if run from granit-dotnet)
  /app-audit showcase             Audit granit-showcase-dotnet
  /app-audit microservice-template  Audit granit-microservice-template
  /app-audit <path>               Audit any host at <path>
  /app-audit help                 This card

FLAGS
  --scope <category>   wiring|endpoints|efcore|persistence|middleware|host|
                       auth|config|localization|providers|notifications|all
  --fix                Apply mechanical fixes (default: report only)

WHAT IT CHECKS  (full list in checklist.md)
  1. Package→wiring matrix   every Granit.* ref is registered/mapped/backed
  2. Endpoints               *.Endpoints ref ⇒ MapGranit* call + [DependsOn]
  3. EF Core                 *.EntityFrameworkCore ref ⇒ AddGranit*EntityFrameworkCore
                             + a migration covering its tables (isolated or folded)
  4. Persistence             GranitDbContext, OnGranitModelCreating, AddGranitDbContext
  5. Host bootstrap          AddGranitAsync<TRoot> / UseGranitAsync / --migrate / health
  6. Middleware order        exception→headers→authn→tenancy→cache→authz→ratelimit
  7. Auth                    Jwt/Keycloak/OpenIddict consistent; permission-based only
  8. Config                  every module's appsettings section present & bound
  9. Localization            languages configured; MapGranitLocalization; culture parity
 10. Providers/Notifications S3/Vault/MaxMind/Brevo/SMTP/channels registered

OUTPUT  severity-ranked findings with file:line and the exact missing call.
```

---

## Methodology

Work in five passes. Be systematic — the value is in completeness, not cleverness.

### Pass 0 — Detect the shape and inventory

1. **Resolve the target.** Map `showcase` → `~/dev/granit-fx/granit-showcase-dotnet`,
   `microservice-template` → `~/dev/granit-fx/granit-microservice-template`, else treat
   the argument as a path. Confirm it is a git repo with a `.slnx`/`.sln`.
2. **Detect the shape.** Microservices iff there are ≥2 `*/Program.cs` under `src/` that
   each call `AddGranitAsync` (or one `*.AppHost` + multiple `*Service` projects).
   Otherwise modular monolith (single `*.Host`).
3. **Build the package inventory.** Parse every `<PackageVersion>` in
   `Directory.Packages.props` and every `<PackageReference Include="Granit.*">` across
   all `*.csproj`. A package is "referenced" by a project when that project (or one it
   `<ProjectReference>`s transitively, like `*.Shared.Hosting`) lists it. Group by
   module family (`Granit.BlobStorage`, `Granit.BlobStorage.Endpoints`,
   `Granit.BlobStorage.EntityFrameworkCore`, `Granit.BlobStorage.S3` → family
   `BlobStorage`).
4. **Find the composition roots.** Locate each `Program.cs`, each `*Module.cs`
   carrying `[DependsOn]`, and each isolated DbContext. These are where wiring lives.

### Pass 1 — The package→wiring matrix (the core check)

For **each referenced package**, classify by suffix and assert its companion wiring.
This is mechanical and exhaustive. Use ripgrep over `Program.cs` + `*Module.cs` +
`*Extensions.cs`.

| Package suffix | Required wiring (assert present) | Failure if missing |
| -------------- | -------------------------------- | ------------------ |
| `Granit.{M}` (base) | Module reached by the graph: either `[DependsOn(typeof(Granit{M}Module))]` on a root module **or** an explicit `AddGranit{M}(...)` / `builder.AddGranit{M}(...)` call. (Zero-`*Module` packages like `Granit.MultiTenancy` are pulled by `using` + a soft API call.) | **HIGH** — package paid for, never activated |
| `Granit.{M}.Endpoints` | `(...).MapGranit{Verb}(...)` mounted on the app/route group **and** `[DependsOn(typeof(Granit{M}EndpointsModule))]`. Map name is usually `MapGranit{M}` but can differ (`MapGranitConversations`, `MapGranitUserSessions`, `MapGranitAccount`) — confirm against the package's public `*EndpointRouteBuilderExtensions`. | **HIGH** — endpoints package referenced but **no routes mounted** (the classic silent gap) |
| `Granit.{M}.EntityFrameworkCore` | `AddGranit{M}EntityFrameworkCore(...)` (or the documented variant: `configureShared` + `configureSchemaPerTenant` for tenant-isolated contexts) **and** the module's tables are covered by a migration — either an isolated-DbContext migration **or** folded into a host/tenant DbContext via `Configure{M}Module()` with that DbContext's migration present. | **HIGH** — runtime "no service" or "relation does not exist" |
| `Granit.{M}.{Provider}` (`.S3`, `.HashiCorp`, `.Keycloak`, `.MaxMind`, `.Brevo`, `.Smtp`, `.Stripe`, `.Ollama`, `.Trigram`, `.PuppeteerSharp`, `.MagickNet`, `.Postgres(ql)`, …) | A provider registration: `AddGranit{M}{Provider}()` / `builder.AddGranit{Provider}()` / `[DependsOn(typeof(Granit{M}{Provider}Module))]`. For abstract base modules with multiple providers, exactly the active provider(s) must be wired. | **HIGH** — abstraction with no concrete impl ⇒ no-op resolver or DI failure |
| `Granit.{M}.Notifications` | Reached via `[DependsOn]`; if the app sends notifications, a delivery channel is registered (`AddGranitNotificationsEmail` / `Sms` / `Sse` / `MobilePush` / `Brevo`) and `AddGranitNotificationsEntityFrameworkCore`. | **MED** — handlers load but never deliver |
| `Granit.{M}.BackgroundJobs` | Reached via `[DependsOn]` **and** `AddGranitBackgroundJobsEntityFrameworkCore(...)` present (jobs need the store + scheduler). | **MED** — recurring jobs silently never run |
| `Granit.{M}.Analytics` / `.Dashboards` | Module-scan satellites — `[DependsOn]` only is sufficient; flag if the referenced satellite has no `[DependsOn]`. | **LOW** |

Then run the matrix **in reverse**:

- **Orphan package** — referenced, but no wiring of any kind found ⇒ HIGH (remove the
  ref or wire it).
- **Dangling call** — a `MapGranit*` / `AddGranit*EntityFrameworkCore` call whose
  package is not referenced (rare; usually won't compile, but catch stale `using`s).

Cross-reference the memory note **`project_session_provider_silent_noop`**: a canonical
endpoint returning `200 []` because the backend module was absent from the
`AddGranit` graph is exactly the failure this matrix catches — a referenced
`.Endpoints` with no backing provider/EF module.

### Pass 2 — Host bootstrap & middleware

1. **Bootstrap present and ordered:** `await builder.AddGranitAsync<TRoot>()` (monolith)
   or `await builder.AddGranitAsync(g => g.AddModule<…>())` per service; the root module
   exists and carries the `[DependsOn]` graph. Then `WebApplication app = builder.Build()`,
   then `await app.UseGranitAsync()` **before** any `MapGranit*`.
2. **`--migrate` path:** `if (app.HasGranitMigrateFlag()) { … RunGranitMigrationsAsync(); return; }`
   and `builder.AddGranitMigrateSupport()` somewhere in the graph. Migration runner must
   exit before the normal pipeline.
3. **Middleware order** (apps get this wrong and it causes real bugs — see the
   showcase's cross-tenant output-cache comment). Assert this relative order where the
   pieces are present:
   `UseGranitExceptionHandling` → `UseGranitResponseCompression` → `UseGranitSecurityHeaders`
   → `UseCors` → `UseAuthentication` → (`UseGranitDPoPValidation`) → `UseGranitMultiTenancy`
   → `UseGranitOutputCaching` → `UseAuthorization` → `UseGranitRateLimiting`
   → `UseGranitIdempotency` → `UseGranitApiDocumentation`.
   **FAIL if `UseGranitOutputCaching` precedes `UseGranitMultiTenancy`** (cross-tenant
   cache disclosure) or if authn/authz are out of order.
4. **Health checks:** `AddHealthChecks().AddGranitDbContextHealthCheck<TDb>()` for each
   owned DbContext, plus `AddGranitRedisHealthCheck` / `AddGranitKeycloakHealthCheck` /
   `AddGranitVaultHashiCorpHealthCheck` / `AddRabbitMqHealthCheck` matching the
   referenced infra packages, and `app.MapGranitHealthChecks()`.
5. **OpenAPI:** `app.UseGranitApiDocumentation()` present; native `AddOpenApi` only —
   **flag any Swashbuckle/NSwag reference** (banned by `granit-dotnet/CLAUDE.md`).

### Pass 3 — Persistence

1. **DbContext base:** a DbContext owning ≥1 `IMultiTenant` entity **must** inherit
   `GranitDbContext` and forward `(options, currentTenant, dataFilter)`. With no tenant
   entity, `DbContext` or `GranitDbContext` are both allowed — but the choice should be
   deliberate (comment). Override **`OnGranitModelCreating`**, never `OnModelCreating`,
   on `GranitDbContext` derivatives; no manual unnamed `HasQueryFilter`.
2. **Registration:** `AddGranitDbContext<T>` / `AddGranitIsolatedDbContext<T>` (Scoped,
   interceptors auto-wired) — **flag bare `AddDbContext`/`AddDbContextFactory`** for a
   Granit DbContext (loses `AuditedEntityInterceptor`/`SoftDeleteInterceptor` and the
   parameterised tenant filter).
3. **Migrations:** a `Migrations/` folder exists with at least an `Init` + a current
   model snapshot for every owned DbContext; for **folded** modules confirm the
   `Configure{M}Module()` call exists in the host/tenant DbContext's `OnGranitModelCreating`.
   Note (do not fail) if the model snapshot looks stale vs the entity set.
4. **Provider:** PostgreSQL/Npgsql is the Granit default; `UseVector()` is mandatory
   wherever `Granit.Indexing*` or a `vector` column is folded in.
5. **Microservices:** each service owns its own DB/connection string — **flag a shared
   DbContext or a connection string reused across services.**

### Pass 4 — Auth, config, localization, layer purity

1. **Auth consistency:** the referenced auth stack is wired coherently —
   `Granit.Authentication.JwtBearer(.Keycloak)` ⇒ `UseAuthentication` + bearer scheme;
   `Granit.OpenIddict*` ⇒ `builder.AddGranitOpenIddict(...)` plus
   `app.MapGranitOpenIddictServer(...)` plus EF stores; `Granit.Bff*` ⇒
   `UseGranitBffTokenInjection` + `MapGranitBff`.
   **Permission-based authorization only — flag any `RequireRole`** (incl. tests; see
   memory `feedback_never_requirerole_permission_based`).
2. **Config (appsettings.json + per-environment):** for each referenced module assert
   its config section exists and is non-empty when required — `ConnectionStrings`,
   `Http:Cors`, `Http:ApiDocumentation`, `RateLimiting`, `Wolverine:Postgresql`,
   `MultiTenancy:TenantIsolation`, `OpenIddict:Seeding`, `Notifications`, `BlobStorage`,
   `Vault`, `Cache`, `IpGeolocation:MaxMind`, `Authentication:External`, etc. **No
   hardcoded secrets / PII** (security baseline); production secrets via Vault.
3. **Localization:** `GranitLocalizationOptions.Languages` configured; if
   `Granit.Localization.Endpoints` referenced, `MapGranitLocalization()` mounted.
   For **app-owned** resource files under `**/Localization/**/*.json`, the 18-culture
   parity rule applies (15 base + 3 regional) — flag missing culture files.
4. **Layer purity in app modules** (showcase `*.Modules.*`): the same STRICT rules as
   the framework — types touching `Microsoft.AspNetCore.*`/FluentValidation belong in an
   endpoints layer, EF-Core-only types in a persistence layer, domain orchestration in
   the base. Endpoints use named `private static` handlers + the 5 OpenAPI metadata
   elements + `.WithTags("Title Case")`; every `*Request` has a `GranitValidator<T>`.

### Severity & report

Classify every finding:

- **HIGH** — silent no-op or runtime failure: referenced package not wired, endpoints
  not mounted, EF module not registered/migrated, provider missing, middleware order
  causing a security bug (cross-tenant cache, authz-before-authn), `RequireRole`,
  Swashbuckle, shared DB across microservices, hardcoded secret.
- **MED** — degraded behaviour: notifications with no channel, background jobs with no
  store, missing health check, bare `AddDbContext`, missing config section with a safe
  default, missing localization culture file.
- **LOW** — hygiene: orphan `using`, satellite without `[DependsOn]`, stale snapshot,
  documentation/comment gaps.

Output format:

```text
Granit Integration Audit — <repo> (<shape>)
Packages: <n> Granit.* referenced across <m> projects

── Package → wiring matrix ───────────────────────────────
✅ BlobStorage         base✓  endpoints✓(MapGranitBlobStorage @Program.cs:529)  efcore✓  s3✓
⚠️  Notifications       base✓  efcore✓  channels: Email,Sse ✓   ← MobilePush referenced, not registered
❌ Scheduling.Endpoints referenced @csproj  but no MapGranitScheduling found  [HIGH]
❌ Webhooks.EntityFrameworkCore referenced  but AddGranitWebhooksEntityFrameworkCore missing  [HIGH]

── Host & middleware ─────────────────────────────────────
❌ UseGranitOutputCaching precedes UseGranitMultiTenancy (Program.cs:NNN) — cross-tenant cache leak [HIGH]

── Findings (by severity) ────────────────────────────────
[HIGH] … file:line … → exact missing call to add
[MED]  …
[LOW]  …

Summary: X HIGH, Y MED, Z LOW.  Suggested next: /app-audit <repo> --fix --scope wiring
```

Lead with the matrix (it is what the user asked for — "point by point, are the
endpoints present, are the EF Core modules present if the module is present"), then the
ordered findings. Be specific: every finding cites a `file:line` and the **exact**
call to add or move.

### --fix mode

Only mechanical, unambiguous fixes:

- Add a missing `MapGranit{X}()` in the right group, mirroring sibling calls + comment.
- Add a missing `[DependsOn(typeof(Granit{X}…Module))]` in alphabetical position.
- Add a missing `AddGranit{X}EntityFrameworkCore(opts => opts.UseNpgsql(cs))` next to
  its peers in the infrastructure module.
- Reorder middleware to the canonical sequence.

**Never** invent connection strings, secrets, migrations, or provider config — report
those as manual follow-ups. After fixing, build the affected project (single project /
`.slnf` shard — never the full solution) and re-run the matrix to confirm green.

---

The exhaustive, copy-pasteable checklist (every check with its ripgrep query, pass
criterion, severity, and rationale) lives in [`checklist.md`](checklist.md). Read it
before a full audit.
