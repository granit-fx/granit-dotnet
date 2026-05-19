# CLAUDE.md - Granit

## Project

- **Type**: Modular .NET framework (~128 packages, ~134 test projects)
- **Repo**: `granit-dotnet` (open-source, Apache-2.0)
- **Compliance**: GDPR + ISO 27001
- **Stack**: .NET 10 (LTS) | C# 14 | EF Core 10 | VaultSharp 1.17+ | Serilog 9+ | OpenTelemetry 1.15+

## Architecture

```text
src/
  Granit/                         # Module system, shared domain types
  Granit.{Module}/                # Abstractions, DI, *Module class, *Definitions, Diagnostics
  Granit.{Module}.Endpoints/      # HTTP-only: Minimal API, *Request/*Response, validators, permissions
  Granit.{Module}.EntityFrameworkCore/  # Data-only: isolated DbContext, configs, interceptors, executors
  Granit.{Module}.{Provider}/     # Provider impls (.S3, .Keycloak, ...)
  Granit.{Module}.Wolverine/      # Wolverine handlers, sagas
  Granit.{Module}.Notifications/  # Notification channel integration
  Granit.{Module}.BackgroundJobs/ # IBackgroundJob records + handlers
  bundles/Granit.Bundle.{Name}/   # Meta-packages
tests/{*.Tests, *.Tests.Integration, ArchitectureTests}
```

> 📚 **Doc site** lives in the sibling repo [`granit-docs`](https://github.com/granit-fx/granit-docs) (`~/dev/granit-fx/granit-docs/`, published to <https://granit-fx.dev>).

One project = one NuGet package. Namespace = project name. Zero circular refs. Discover packages via `ls src/`.

**Layer purity — STRICT.** `.Endpoints` belongs only to types touching `Microsoft.AspNetCore.*`,
`FluentValidation`, `ZiggyCreatures.Caching.Fusion`, or HTTP-bound options. `.EntityFrameworkCore`
only to types touching `Microsoft.EntityFrameworkCore`. Domain orchestration (registries, runners,
period resolvers, delta calculators, value-shape DTOs, declarative definitions) lives in the base
`Granit.{Module}`. Enforced by `/audit --scope layer-purity`.

## Commands

> ⚠️ **NEVER `dotnet build`/`test`/`format` on the full solution.** Roslyn OOMs. Always target a `.slnf` shard or single project.

```bash
# Shards (CI parity)
dotnet build  .github/shard-filters/<shard>.slnf
dotnet test   .github/shard-filters/<shard>.slnf --no-build
dotnet format .github/shard-filters/<shard>.slnf --verify-no-changes

# Single package (tight feedback)
dotnet build src/Granit.BlobStorage
dotnet test  tests/Granit.BlobStorage.Tests --no-build

# Pack
dotnet pack -c Release -o ./nupkgs
```

**Dev-package feed.** Pre-release `0.1.0-dev.*` packages publish to the **GitLab Package Registry** (`gitlab.digitaldynamics.be/api/v4/projects/11/packages/nuget/index.json`) — GitHub Packages publish is paused (#2114). Downstream repos consume from there; CI creds via env var `NuGetPackageSourceCredentials_*` (never `--store-password-in-clear-text`).

Doc site lives in the [`granit-docs`](https://github.com/granit-fx/granit-docs) sibling repo (`cd ../granit-docs && npx astro build`).

Shard mapping is the source of truth at [`.github/test-shards.json`](.github/test-shards.json) — match a file's directory against that map, no duplicate table here.

## Code style

Idiomatic modern C# 14 / .NET 10. Default to: primary constructors, collection expressions
(`[x, y]`, `[]`), `field` keyword, file-scoped namespaces, pattern matching, `System.Threading.Lock`,
`params ReadOnlySpan<T>`, native OpenAPI 3.1 (`AddOpenApi()` — never Swashbuckle/NSwag), Named Query
Filters (`HasQueryFilter(name, expr)` — never unnamed), `IMeterFactory` (never `new Meter`),
`ActivitySource` per module.

`nameof(T)` on a type parameter returns `"T"` — use `typeof(T).Name` instead. `nameof(List<>)` works for unbound generics.

### Must-use patterns

- `var` when type is apparent (IDE0008); expression body for single-statement methods
- `[GeneratedRegex]` always; `[LoggerMessage]` always (no string interpolation in log calls)
- `TimeProvider` / `IClock` — never `DateTime.Now`/`UtcNow`
- `ConfigureAwait(false)` in library code; `CancellationToken` last param
- `ArgumentNullException.ThrowIfNull()` / `ArgumentException.ThrowIfNullOrEmpty()`
- `AddAuthorizationBuilder()` — not `AddAuthorization(Action<>)` (ASP0025)

## Conventions

Full reference: [`docs/guide/conventions/`](docs/guide/conventions/index.md).

### Diagnostics

- `Diagnostics/` folder in base module. `sealed class {Module}Metrics` with `IMeterFactory` ctor.
- Meter name `"Granit.{Module}"` (PascalCase). Metric name `granit.{module}.{entity}.{action}` (lowercase). Tags `snake_case`, always include `tenant_id` (coalesced to `"global"`).
- DI: `services.TryAddSingleton<{Module}Metrics>()`.
- `internal static class {Module}ActivitySource` registered via `GranitActivitySourceRegistry.Register(Name)`.

### Permissions (STRICT)

Format `[Group].[Resource].[Action]` (PascalCase, plural Resource). Loc keys: `PermissionGroup:{Group}` and `Permission:{Group}.{Resource}.{Action}`. Provider class `{Module}PermissionDefinitionProvider : IPermissionDefinitionProvider` (auto-discovered). Standard actions: `Read`, `Manage`, `Execute`, `Create` — domain-specific (`Upload`, `Revoke`, `Rotate`...) allowed when `Manage` is too coarse for least-privilege (ISO 27001 A.9.4).

### Events (STRICT, archi-tested)

| Suffix | Interface | Dispatch | Example |
| ------ | --------- | -------- | ------- |
| `*Event` | `IDomainEvent` (or none) | `AddDomainEvent()` / `ILocalEventBus` | `BlobValidatedEvent` |
| `*Eto` | `IIntegrationEvent` | `AddDistributedEvent()` / `IDistributedEventBus` (Wolverine outbox) | `PersonalDataDeletedEto` |

Past-tense verb + suffix. Generic lifecycle: `EntityCreatedEvent<T>`/`EntityCreatedEto<T>`.

### Wolverine handlers — CRITICAL

Wolverine discovers via `Assembly.ExportedTypes` and needs **public types with public ctors**.

- Handlers MUST be `public class` (non-static) with `public static` Handle methods. NEVER `static class`, NEVER `internal`, NEVER `protected` ctor.
- If a handler injects an `internal` service, make the service public or extract an interface.
- SonarQube **S1118** is a false positive on these — mark **Won't Fix**.

### Background Jobs

- `sealed record *Job : IBackgroundJob` with `[RecurringJob("cron", "name")]`, lives in `Granit.{Module}.BackgroundJobs/Jobs/`. Job name `{module-kebab}-{action-kebab}` (globally unique).
- Handler `{Action}Handler` — `public static partial class`. Same Wolverine visibility rules.
- Dedicated `Granit.{Module}.BackgroundJobs` sub-project keeps base module free from `Granit.BackgroundJobs` dep. Never create a `.Wolverine` package for jobs (handled by `Granit.BackgroundJobs.Wolverine`).
- NEVER use `*Command` suffix — commands are CQRS, jobs are scheduled work units.

### `*.Notifications` packages

Full conventions: [notifications/conventions.mdx](../granit-docs/src/content/docs/dotnet/infrastructure/notifications/conventions.mdx) (also at <https://granit-fx.dev/dotnet/infrastructure/notifications/conventions/>). Reference impls: `Granit.Privacy.Notifications`, `Granit.Identity.Local.Notifications`.

Critical gotchas:

- `<EmbeddedResource Include="Templates\**\*.html" WithCulture="false" />` — without `WithCulture="false"` MSBuild treats `*.fr.html` as satellite resources.
- `*Display` companion field MANDATORY for `IReadOnlyList<T>` data fields — JSON arrays don't iterate in Scriban after `JsonElementToDictionary` flattening. Pattern: `string MissingProvidersDisplay = string.Join(", ", MissingProviders)`.
- Template first line MUST be `<title>...</title>` — email channel extracts subject from it.
- Notification type `Name`: `snake_case` preferred (`privacy.deletion_acknowledged`). Singleton `Instance` referenced by handlers.
- Wolverine routing — explicitly state local (`ILocalEventBus`, in-process) vs distributed (`*Eto` over bus) per story; default-to-distributed for in-process events wastes a round trip.
- Tests ship a manifest pinning theory test (one row per (notification, culture)) — copy from `Granit.Privacy.Notifications.Tests/Templates/EmbeddedTemplatesTests.cs`.
- Framework baseline ships **EN + FR**. Other cultures generated by `scripts/translate-templates.py` with `<!-- AUTO-TRANSLATED -->` marker.

> **Analytics / Dashboards conventions** (MetricDefinition, widget references,
> Query↔Metric pairing) moved to `granit-business` along with the
> `Granit.Analytics.Abstractions` and `Granit.Dashboards.Abstractions` packages
> — those contracts are business concerns, not framework. See the
> granit-business `CLAUDE.md` for the canonical rules.

### Declarative definitions placement (Query/Export)

Concrete `*QueryDefinition` and `*ExportDefinition` live in **base module** `Granit.{Module}` (in `Queries/`, `Exports/`). NEVER in `.Endpoints`, NEVER in `.EntityFrameworkCore`. Reference `Granit.{X}.Abstractions` (lightweight contracts), never `Granit.QueryEngine`/`Granit.DataExchange` directly. Each module owns its registrations — NEVER aggregate into a central `Granit.{X}.Definitions` package.

**Pairing (STRICT, archi-tested):** every admin-visible entity MUST have both `QueryDefinition` AND `ExportDefinition`. Exemption list (`[INFRA]`): `tests/Granit.ArchitectureTests/PairingExemptions.cs` — each entry carries an inline justification.

### DTOs & API responses

- `*Request` for input, `*Response` for output. NEVER `*Dto`. Prefixed names (`WorkflowTransitionRequest`, not `TransitionRequest`) — OpenAPI flattens namespaces.
- Errors: `TypedResults.Problem(detail, statusCode)` (RFC 7807). EF entities NEVER returned — always project to `*Response` records.

### OpenAPI endpoint metadata (5 elements MANDATORY)

```csharp
group.MapGet("/{id:guid}", GetByIdAsync)
    .WithName("GetBlobDescriptor")                       // PascalCase VerbNoun
    .WithSummary("Returns a blob descriptor by ID.")     // imperative, ~100 chars, period
    .WithDescription("Fetches the full metadata...")     // 2-4 sentences
    .Produces<BlobDescriptorResponse>()                  // match handler return type
    .ProducesProblem(StatusCodes.Status404NotFound);     // one per error path
```

`Ok<T>`→`.Produces<T>()`, `Created<T>`→`.Produces<T>(201)`, `NotFound`→`.ProducesProblem(404)`, `ValidationProblem`→`.ProducesValidationProblem()`, `FileStreamHttpResult`→`.Produces(200, contentType: "application/octet-stream")`.

### OpenAPI tags (STRICT)

Every `*.Endpoints` module attaches `.WithTags(...)` on its root group. Format: `Title Case With Spaces` (e.g. `Blob Storage`, `Background Jobs`) — NEVER glued PascalCase, kebab-case, or snake_case. Multi-tag modules: `<Module> - <SubGroup>` (space-dash-space) — e.g. `AI - Workspaces`, `Identity - Webhook`. Expose via `TagName` property on `*EndpointsOptions` (overridable per-app). `Granit.Http.ApiDocumentation` auto-emits a sorted `document.Tags` array.

### Validation

- `endpoints.MapGranitGroup(prefix)` — applies `FluentValidationAutoEndpointFilter` automatically.
- Validators auto-discovered by `GranitValidationModule`. Opt-out: `WithMetadata(new SkipAutoValidationAttribute())`.
- `FluentValidationSchemaTransformer` exposes constraints (maxLength, pattern...) in OpenAPI for codegen.
- **Localized messages MANDATORY**: never hardcode `.WithMessage("...")`. Built-ins (NotEmpty, MaximumLength) auto-converted to error codes by `GranitErrorCodeLanguageManager`. Custom `.Must()` → `.WithErrorCodeAndMessage("Granit:Validation:XxxCode")` + add the key to all 17 JSON files in `src/Granit.Validation/Localization/Validation/`.

### Isolated DbContext (each `*.EntityFrameworkCore` package)

1. `<ProjectReference>` to `Granit.Persistence` (+ `Granit.Persistence.EntityFrameworkCore`).
2. **If the DbContext owns ≥1 `IMultiTenant` entity → inherit `GranitDbContext`** (forward `options, currentTenant, dataFilter` to base). The base class exposes `CurrentTenantId` / `IsMultiTenantFilterEnabled` as instance members so EF Core parameterises the tenant filter (`@ef_filter__CurrentTenantId`) instead of inlining a frozen constant — a closure-captured tenant leaks across requests (repro: `MultiTenantFilterParameterizationReproTests.cs`). For DbContexts with no `IMultiTenant` entity, inherit `DbContext` directly.
3. Constructor-inject `ICurrentTenant?` and `IDataFilter?` (both optional, default `null`).
4. Override `OnGranitModelCreating` (not `OnModelCreating`) on `GranitDbContext` derivatives. Call `modelBuilder.ApplyGranitConventions(currentTenant: null, dataFilter)` — the tenant filter is wired by the base; the other conventions (soft-delete, active, …) still come from `ApplyGranitConventions`. NO manual `HasQueryFilter`.
5. Wire interceptors via the `(sp, options)` overload of `AddDbContextFactory` (Scoped) — resolve `AuditedEntityInterceptor` / `SoftDeleteInterceptor`.
6. `[DependsOn(typeof(GranitPersistenceModule))]` on module class.
7. `IMultiTenant` entities use `Guid? TenantId` (never `string`).

Reference: [`docs/framework/data/persistence.md`](docs/framework/data/persistence.md).

### DDD — AggregateRoot vs Entity

`AggregateRoot` (or audited variants) when the entity has a state machine, raises domain events, or encapsulates invariants. Plain `Entity` for append-only / config / lookup.

Aggregate Root rules (enforced by `DomainConventionTests`):

- All properties `{ get; private set; }`. State changes via behavior methods (`MarkAsValid()`, `Revoke()`).
- `public static Xxx Create(...)` factory + `private Xxx() { }` for EF Core materialization.
- For `IMultiTenant` with private setter: add explicit `Guid? IMultiTenant.TenantId { get; set; }` for interceptor injection.
- Events via base class (`AddDomainEvent`/`AddDistributedEvent`) — NEVER manual `IDomainEventSource`.

`SingleValueObject<T>`: `sealed`, `init` props, `Create()` with validation, implicit operators. EF converters auto-applied by `ApplyGranitConventions`. JSON via `SingleValueObjectJsonConverterFactory`. Reference: [ADR-017](../granit-docs/src/content/docs/dotnet/architecture/adr/017-ddd-aggregate-value-object-strategy.md).

### Multi-tenancy (soft dep)

`ICurrentTenant` lives in `Granit.MultiTenancy` — `using Granit.MultiTenancy;` everywhere without `[DependsOn]`. Always check `IsAvailable` before `Id` (`NullTenantContext` is the default). Hard `[DependsOn]` only when enforcing strict tenant isolation (GDPR).

### `[DependsOn]` rules

Direct project ref with a `*Module` → declare it. Transitive → omit. `Granit` is the implicit base — never declared. Alphabetical order. Zero-dep modules have no attribute (correct).

### Tests + CI sharding (MANDATORY)

**Endpoint harness.** For `*.Endpoints` HTTP tests, use `Granit.Testing.Endpoints.GranitEndpointTestHost` — the canonical harness; replaces ~60 lines of `WebApplicationBuilder` boilerplate (reference impl: `Granit.Validation.Endpoints.Tests`, introduced in #2132).

8 parallel shards (6 unit-test layers + `integration` + `architecture`). Each has a `.slnf` (auto-generated). When adding a test project: edit `.github/test-shards.json` (`*.Tests.Integration` → `integration` shard always; everything else → its domain shard), run `python3 scripts/generate-shard-filters.py`, commit both. Pre-push hook auto-regenerates and amends. **Without registration, CI silently skips the project.**

## Anti-patterns

- `new HttpClient()` → `IHttpClientFactory`. `async void` → `Task`. `.Result`/`.Wait()` → `await`. Bare `catch (Exception)` → catch specific.
- Repository pattern over EF Core → use DbContext directly. Cross-module direct calls → integration events. Shared DbContext → isolated per module. Merging `I*Reader`/`I*Writer` for SonarQube → keep CQRS, mark won't fix.
- Removing/changing interface impls on ValueObject/Entity/AggregateRoot for SonarQube → won't fix. Param-count refactors via fake wrapper types → won't fix `brain-overload`.

### Git — CRITICAL

**NEVER `git push` to a PR branch without `gh pr view <n> --json state -q .state` first.** If not `"OPEN"`: branch from develop, cherry-pick, new PR. BLOCKING.

## Localization

15 base + 3 regional = 18 cultures (en, fr, nl, de, es, it, pt, zh, ja, pl, tr, ko, sv, cs, hi + fr-CA, en-GB, pt-BR). Every `src/*/Localization/**/*.json` exists for all 18; regional files contain only differing keys. Full rules: [`docs/guide/conventions/langues.md`](docs/guide/conventions/langues.md).

## Documentation site

Lives in the sibling repo [`granit-docs`](https://github.com/granit-fx/granit-docs) (`~/dev/granit-fx/granit-docs/`, Astro + Starlight, published to <https://granit-fx.dev>). When creating a new module: add `.mdx` in `reference/modules/`, bump `PACKAGE_COUNT` in `src/data/constants.ts`, cross-link from related pages. **Doc updates ship in a separate PR against `granit-docs`** — keep code and docs PRs decoupled.

## MCP & Code index

MCP tools (roslyn-lens, granit-tools) — see global `~/.claude/CLAUDE.md`. `.mcp-code-index.json` is auto-regenerated by the pre-push hook (script: `python3 scripts/generate-code-index.py`). **NEVER edit manually.**

## Definition of Done

See [`docs/guide/conventions/dod.md`](docs/guide/conventions/dod.md). NEVER push or open a PR without: tests passing, docs updated, `dotnet format --verify-no-changes` clean, markdownlint clean. **Blocking.**
