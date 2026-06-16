# Granit .NET — Audit Checklist

Verification matrix used by the `/audit` skill. Each category maps to a
`--scope` value. Apply in order.

Convention references point to:

- `CLAUDE.md` — project conventions (root of repo)
- `docs-site/…/dotnet/` — Astro documentation site, now the **sibling repo**
  `../granit-docs/` — resolve any `docs-site/…/X` reference as
  `../granit-docs/src/content/docs/dotnet/…/X`
- `GRXXXX` — Roslyn analyzer rule

---

## 1. Module anatomy (`--scope anatomy`)

### 1a. Layered split

- [ ] Base project `Granit.{Module}` exists with interfaces, options, DI extension
- [ ] Module class inherits `GranitModule` and is `public sealed`
- [ ] `[DependsOn]` attributes match actual `<ProjectReference>` graph (direct only,
  transitive omitted, alphabetical order)
- [ ] `Granit` is never listed in `[DependsOn]` (implicit base)
- [ ] Zero-dependency modules have no `[DependsOn]` attribute
- [ ] `.Endpoints` project exists if module exposes HTTP API
- [ ] `.EntityFrameworkCore` project exists if module has persistence
- [ ] Provider projects (`.S3`, `.AzureBlob`, etc.) implement abstractions only

Ref: `docs-site/…/concepts/module-system.mdx`

### 1a-bis. Layer purity — `.Endpoints` and `.EntityFrameworkCore` are NOT bins for domain code (STRICT)

The `.Endpoints` and `.EntityFrameworkCore` packages are **single-purpose** layers.
Domain code — orchestration, registries, value-shape DTOs that are not wire envelopes,
diagnostics meters, generic interfaces, pure functions — belongs in the **base module**
`Granit.{Module}`. Detect any leakage and flag with severity `ARCHITECTURE`.

**`.EntityFrameworkCore` MUST contain ONLY data-layer code:**

- Isolated `DbContext` and entity type configurations
- EF Core migrations
- EF Core interceptors and value converters
- `IQueryable<T>` consumers (anything calling `SumAsync` / `AverageAsync` /
  `ExecuteUpdate` / `ExecuteDelete` / `Include` / `AsNoTracking` etc.)
- Implementations of contracts where the runtime touches `Microsoft.EntityFrameworkCore`
- The `Add{Module}EntityFrameworkCore` DI extension and module class

Anything else is a domain leak — flag and recommend moving to `Granit.{Module}`. Common offenders:

- `*Service` registry classes (`IEnumerable<I*Runner>` → name dictionary) — pure
  orchestration, no EF Core; **belongs in `Granit.{Module}/Internal/`**
- Diagnostic meter wrappers (`*RuntimeMetrics` using `IMeterFactory`) — no EF Core;
  **belongs in `Granit.{Module}/Diagnostics/`**
- Pure interfaces (`I*Executor`, `I*Runner`, `I*Reader`) without EF Core types in
  signatures — **belongs in `Granit.{Module}/`** (often in `Internal/` or a domain
  subfolder like `Metrics/`)
- Pure helpers / translators (`DashboardFilterTranslator`, `*Translator`) operating on
  primitives + abstractions only — **belongs in `Granit.{Module}/Internal/`**
- Snapshot / DTO records consumed by the domain (not by the wire) — **belongs in the
  base module**

**`.Endpoints` MUST contain ONLY HTTP-layer code:**

- Minimal API route handlers (`Map*` methods)
- HTTP wire DTOs (`*Request`, `*Response` envelopes that round-trip via JSON)
- FluentValidation validators (`AbstractValidator<TRequest>`)
- `IPermissionDefinitionProvider` and `*Permissions` constants
- HTTP-specific options (`*EndpointsOptions`, route prefix, tag name, FusionCache TTL)
- `Add{Module}Endpoints` / `MapGranit{Module}` DI + route extensions
- HTTP localization resources (permission strings, error messages)
- HTTP cache key composers (FusionCache, response cache)
- Orchestrators that depend on FusionCache, `IFluentValidator`, ASP.NET Core types,
  or HTTP options

Anything else is a domain leak — flag and recommend moving to `Granit.{Module}`. Common offenders:

- Widget instance renderers (`*WidgetInstanceRenderer`), datasource evaluators
  (`I*Evaluator`, non-HTTP `*Evaluator`) — produce snapshots consumed by both HTTP
  and non-HTTP renderers; **belong in `Granit.{Module}/Rendering/`**
- Snapshot records (`*WidgetSnapshot`, `*Payload`) consumed by renderers — domain
  shape; **belong in `Granit.{Module}/Rendering/`** (only the wire envelope record
  that wraps them stays in `.Endpoints/Dtos/`)
- Pure helpers (`PeriodResolver`, `*FilterBuilder`, `DeltaCalculator`) using only
  `IClock` / `Expression<>` / framework primitives — **belong in
  `Granit.{Module}/Internal/`**
- Non-generic dispatch interfaces (`IMetricRunner`) and their typed implementations
  (`MetricRunner<,>`) — domain contracts; **belong in `Granit.{Module}/Internal/`**

**Detection strategy:**

1. List every `.cs` file in `src/Granit.{Module}.Endpoints/` and
   `src/Granit.{Module}.EntityFrameworkCore/` (excluding the module class and the
   DI extension classes).
2. For each file, list its top-level imports (`using` directives) excluding `System.*`.
3. Flag any file whose imports are **all** in this set:
   - For `.EntityFrameworkCore`: any file with NO `using Microsoft.EntityFrameworkCore`
     and NO direct EF Core API call (`DbSet<>`, `SumAsync`, `ExecuteUpdate`, etc.) is
     a candidate for `Granit.{Module}/`. Confirm by reading the type body.
   - For `.Endpoints`: any file with NO `using Microsoft.AspNetCore.*`, NO
     `using FluentValidation`, NO `using ZiggyCreatures.Caching.Fusion`, and no other
     HTTP/validator dependency is a candidate for `Granit.{Module}/`. Confirm by
     reading the type body — files referencing types from `Granit.{Module}.Endpoints.Options`
     or HTTP-bound services (e.g. `MetricEndpointService`) legitimately stay.
4. Cross-check by grepping consumers in non-HTTP / non-data hosts (background jobs,
   future push transports, IoT workers). A type that consumers _outside_ the HTTP
   pipeline need is a strong signal that it belongs in the base module.

**Severity guidance:**

| Finding | Severity |
| ------- | -------- |
| Pure orchestration class (no EF Core / no HTTP) in `.EntityFrameworkCore` or `.Endpoints` | ARCHITECTURE |
| Public interface without layer-specific types in `.EntityFrameworkCore` or `.Endpoints` | ARCHITECTURE |
| Snapshot / payload record consumed by domain renderers placed in `.Endpoints/Dtos/` | CONVENTION |
| DI registration of domain types living in `.Endpoints` or `.EntityFrameworkCore` extension methods | ARCHITECTURE (move to base module's `Add{Module}()`) |
| Non-HTTP datasource evaluator (`I*DatasourceEvaluator`, `*DatasourceEvaluator`) in `.Endpoints/Rendering/` when it has no HTTP dependency | ARCHITECTURE |

Ref: `CLAUDE.md §Architecture`, `CLAUDE.md §Module anatomy`

### 1b. Project structure

- [ ] File-scoped namespaces (`namespace X;`) — no brace-wrapped namespaces
- [ ] Namespace matches project name
- [ ] One primary type per file (file name = type name)
- [ ] `Internal/` folder for non-public implementation types
- [ ] `Diagnostics/` folder for metrics and activity source (if applicable)
- [ ] `Jobs/` folder for background jobs (if applicable)
- [ ] No cross-module internal references (GRMOD001 — compile error)

#### `.Endpoints` internal file layout (STRICT)

Every `*.Endpoints` project with more than 2 endpoints MUST follow the three-layer split:

- [ ] `Extensions/*EndpointRouteBuilderExtensions.cs` is a **thin orchestrator only**
  (~20–80 lines): creates the `RouteGroupBuilder`, sets auth/tags, and calls
  `group.Map{Domain}ReadEndpoints()` / `group.Map{Domain}WriteEndpoints()` (or other
  domain splits). It MUST NOT contain `async` handler methods.
- [ ] `Endpoints/` subfolder exists with `internal static class *Endpoints` files —
  one file per logical domain group (e.g. `TemplatingCrudEndpoints`,
  `PrivacyDeletionEndpoints`). Handler methods live here, not in the extension class.
- [ ] `Internal/*ResponseMapper.cs` exists when handlers share mapping helpers
  (`ToResponse`, `*NotFound`, guard helpers). Never duplicate mapping logic across
  two handler files.

**Detection:**

```bash
# Flag any *EndpointRouteBuilderExtensions.cs that contains async handlers
grep -lE 'private static async Task<' src/*/Extensions/*EndpointRouteBuilderExtensions.cs
```

Any match is a `CONVENTION` finding — refactor following the pattern in
`Granit.Auditing.Endpoints` (reference impl).

**Severity guidance:**

| Finding | Severity |
| ------- | -------- |
| Async handler method directly in `*EndpointRouteBuilderExtensions.cs` | CONVENTION |
| Extension class > 100 lines (handlers likely inlined) | CONVENTION |
| No `Endpoints/` folder when module has ≥ 3 endpoints | CONVENTION |

Ref: `CLAUDE.md §Architecture`, `docs-site/…/core/analyzers.mdx`

### 1c. DI registration

- [ ] `Add{Module}(this IServiceCollection)` extension method exists
- [ ] Extension method is in `Microsoft.Extensions.DependencyInjection` namespace
- [ ] `TryAdd*` used (not `Add*`) to allow consumer overrides
- [ ] `GranitActivitySourceRegistry.Register(Name)` called if module has tracing
- [ ] No extension method parameters for config (use `appsettings.json` / Vault)
  — exception: `Action<DbContextOptionsBuilder>` for EF Core
- [ ] Correct lifetime: Singleton for stateless, Scoped for request/user/tenant,
  Transient only for lightweight disposable types

Ref: `docs-site/…/concepts/dependency-injection.mdx`

### 1d. Options pattern

- [ ] Options class is `sealed class` with `const string SectionName`
- [ ] Bound via `BindConfiguration(SectionName)`
- [ ] `ValidateDataAnnotations()` for constraint validation
- [ ] `ValidateOnStart()` for fail-fast on misconfiguration
- [ ] No secrets in `appsettings.json` — use env vars, User Secrets, or Vault

#### `SectionName` convention (STRICT)

Format: `{Module}[:{SubModule}[:{Feature}]]` — colon-separated, ASP.NET-style.
Aligned on the project namespace after stripping the `Granit` prefix.
Example: `Granit.Foo.Bar.Baz` → `"Foo:Bar:Baz"`.

- [ ] **Never starts with `Granit:`** — `Granit` is the code namespace, not a
  configuration root. `"Granit:ApiKeys"` → `"Authentication:ApiKeys"`,
  `"Granit:IO:TempFiles"` → `"IO:TempFiles"`, etc.
- [ ] **No PascalCase-glued tokens** — `"FooBar"` is forbidden whenever
  `"Foo:Bar"` applies. Specifically forbidden patterns:
  - `*Endpoints` → split as `*:Endpoints` (e.g. `BlobStorageEndpoints` →
    `BlobStorage:Endpoints`)
  - `Wolverine*` → split as `Wolverine:*` (e.g. `WolverinePostgresql` →
    `Wolverine:Postgresql`)
  - `GranitMigrations` → `Persistence:Migrations`
  - `TenantSchema` → `MultiTenancy:TenantSchema`
- [ ] **Single-segment SectionName** is allowed ONLY for top-level modules whose
  project is the root namespace (`Notifications`, `Authentication`, `Vault`,
  `BlobStorage`, `Cache`, `Bff`, …). Any compound concept MUST be hierarchical.
- [ ] **Aligned with namespace** — a sub-project under `Granit.Foo.Bar` should
  bind to `"Foo:Bar"`, never invent a new root (`"AzureCommunicationServices:Email"`
  for `Granit.Notifications.Email.AzureCommunicationServices` is wrong — should be
  `"Notifications:Email:AzureCommunicationServices"`).
- [ ] **No collision** — two `Options` classes never share the same SectionName
  string. ASP.NET allows a section to host both bound properties AND child
  sub-sections, but two distinct Options classes pointing at the same path
  break binding silently (real incident: `ImportOptions` and the parent
  `DataExchange` root both used `"DataExchange"`).
- [ ] **Test asserts the SectionName** — every `*Options` ships an
  `OptionsTests.cs` with `SectionName.ShouldBe("Expected:Path")` so renames
  surface in CI.
- [ ] **`appsettings.json` examples reflect the current name** — in
  `templates/`, doc XML, and embedded examples.

Enforced by `Granit.ArchitectureTests.SectionNameConventionTests`. When you
need a new top-level section, add it to `AllowedSingleSegmentSections` in that
file along with a one-line rationale.

Ref: `docs-site/…/concepts/configuration.mdx`

---

## 2. Code conventions (`--scope code`)

### 2a. C# 14 / 13 / .NET 10 features

- [ ] Primary constructors for DI service classes (private readonly fields when
  param used in multiple methods)
- [ ] Collection expressions: `[x, y]` not `new[] { x, y }`, `[]` not
  `Array.Empty<T>()` or `new List<T>()`
- [ ] `System.Threading.Lock` for synchronization — never `lock(object)` or
  `lock(this)`
- [ ] `params ReadOnlySpan<T>` over `params T[]` in non-attribute methods
- [ ] `\e` escape for ESCAPE characters (not `\u001b` or `\x1b`)
- [ ] Named query filters: `HasQueryFilter(name, expr)` — never unnamed
- [ ] Pattern matching preferred: `is`, `switch` expressions, list/property patterns

Ref: `CLAUDE.md §C# 14 features`

### 2b. Mandatory patterns

- [ ] `var` when type is apparent; explicit type otherwise
- [ ] Expression body (`=>`) for single-statement methods
- [ ] String interpolation `$"..."` — not `string.Concat` or `string.Format`
- [ ] `[GeneratedRegex]` — never `new Regex(..., Compiled)` — timeout on user input
- [ ] `[LoggerMessage]` — never string interpolation in log calls
- [ ] `TimeProvider` / `IClock` — never `DateTime.Now` / `UtcNow` (GRSEC001)
- [ ] `IGuidGenerator` — never `Guid.NewGuid()` (GRSEC002)
- [ ] `ConfigureAwait(false)` in library code
- [ ] `CancellationToken` as last parameter in async methods
- [ ] `ArgumentNullException.ThrowIfNull()` — not manual null checks
- [ ] `ArgumentException.ThrowIfNullOrEmpty()` / `ThrowIfNullOrWhiteSpace()` for
  strings
- [ ] `TypedResults.*()` — never `Results.*()` (GRAPI001)
- [ ] `IHttpClientFactory` — never `new HttpClient()`
- [ ] `IMeterFactory` — never `new Meter(...)`
- [ ] `IGranitCookieManager` — never direct `IResponseCookies` (GRSEC004)
- [ ] `SaveChangesAsync()` — never `SaveChanges()` (GREF001)
- [ ] No `async void` — always return `Task`
- [ ] No `.Result` / `.Wait()` — always `await`
- [ ] No bare `catch (Exception)` — catch specific types
- [ ] No hardcoded secrets, even in comments (GRSEC003)

Ref: `CLAUDE.md §Must-use patterns`, `docs-site/…/core/analyzers.mdx`

### 2c. Anti-patterns (flag if found)

- [ ] `*Dto` suffix on types (should be `*Request` / `*Response`)
- [ ] `TypedResults.BadRequest<string>()` (should be `TypedResults.Problem()`) (GRAPI002)
- [ ] EF entities returned from endpoints (must map to `*Response` DTOs)
- [ ] `Results.Ok()` instead of `TypedResults.Ok()`
- [ ] `new Meter(...)` instead of `IMeterFactory`
- [ ] Combined `I*Store` interfaces (should be separate `I*Reader` / `I*Writer`)
- [ ] Repository pattern over EF Core
- [ ] Cross-module direct method calls (should use integration events)
- [ ] `nameof(T)` on type parameter (should be `typeof(T).Name`)
- [ ] `Guid.NewGuid()` instead of `IGuidGenerator`
- [ ] `DateTime.Now`/`UtcNow` instead of `IClock`/`TimeProvider`

Ref: `CLAUDE.md §Anti-patterns`

---

## 3. Naming conventions (`--scope naming`)

### 3a. Permissions

- [ ] Three-segment format: `[Group].[Resource].[Action]`
- [ ] Group matches `{Module}Permissions.GroupName` (PascalCase)
- [ ] Resource is a plural noun nested static class
- [ ] Action uses standard verbs: `Read`, `Create`, `Update`, `Delete`, `Manage`,
  `Execute`
- [ ] Never `View` — always `Read`
- [ ] Permission constant: `public const string Read = "{Group}.{Resource}.Read";`
- [ ] Localization keys: `PermissionGroup:{Group}` and
  `Permission:{Group}.{Resource}.{Action}`
- [ ] Provider class: `internal sealed class {Module}PermissionDefinitionProvider
  : IPermissionDefinitionProvider`
- [ ] Permissions attached to roles only — never user-level grants (RBAC strict)

Ref: `CLAUDE.md §Permissions`, `docs-site/…/concepts/security-model.mdx`

### 3b. Events

- [ ] Domain events implement `IDomainEvent` with `*Event` suffix
- [ ] Integration events implement `IIntegrationEvent` with `*Eto` suffix
- [ ] Past-tense verb in event name: `BlobValidatedEvent`, `PersonalDataDeletedEto`
- [ ] No bare past-tense names without suffix
- [ ] Generic lifecycle events: `EntityCreatedEvent<T>` / `EntityCreatedEto<T>`
  via `IEmitEntityLifecycleEvents` marker

Ref: `CLAUDE.md §Events`, `docs-site/…/data/entity-lifecycle-events.mdx`

### 3c. Background jobs

- [ ] `*Job` suffix on job records
- [ ] `sealed record` implementing `IBackgroundJob`
- [ ] `[RecurringJob("cron", "name")]` attribute
- [ ] Job name format: `{module-kebab}-{action-kebab}`
- [ ] Handler: `{Action}Handler` as `internal static partial class`
- [ ] Located in `{Module}/Jobs/` folder — never in a separate `.Wolverine` package

Ref: `CLAUDE.md §Background Jobs`

### 3d. DTOs and API types

- [ ] Prefixed names to avoid OpenAPI collisions: `WorkflowTransitionRequest`,
  not `TransitionRequest`
- [ ] `*Request` suffix for input DTOs
- [ ] `*Response` suffix for output DTOs
- [ ] Never `*Dto` suffix
- [ ] Enum JSON serialization: PascalCase via `JsonStringEnumConverter`
- [ ] Dates: ISO 8601 with timezone (`2025-03-15T10:30:00Z`)
- [ ] Identifiers: UUID v7 with dashes

Ref: `CLAUDE.md §DTOs`, `docs-site/…/architecture/http-conventions.md`

### 3d-bis. `SectionName` homogeneity (`--scope naming`)

Full rule list under §1d. Quick checklist for the naming pass:

- [ ] No SectionName starts with `Granit:` (use bare hierarchical path)
- [ ] No PascalCase-glued SectionName for compound concepts (`FooBar` →
  `Foo:Bar`)
- [ ] `*.Endpoints` SectionName follows `Module:Endpoints` (and
  `Module:Endpoints:Feature` for feature subsets) — never `ModuleEndpoints`
- [ ] Sub-projects under `Granit.Notifications.{Channel}.{Provider}` SectionName
  is `Notifications:{Channel}:{Provider}` (don't skip the channel segment, don't
  invent a vendor root)
- [ ] Sub-projects under `Granit.Identity.Federated.{Provider}` SectionName is
  `Identity:Federated:{Provider}` (don't use the legacy `{Provider}Admin` flat
  form)
- [ ] `Granit.{Family}.{Subprovider}` (Vault, Wolverine, Cache, Mcp, AI, …) →
  `{Family}:{Subprovider}` (Wolverine.Postgresql → `Wolverine:Postgresql`)
- [ ] Every Options class has a unit test asserting `SectionName.ShouldBe(...)`
  with the canonical path; assertion is up to date after any rename
- [ ] `templates/granit-*/appsettings.json` keys match the current SectionName

Enforced by `Granit.ArchitectureTests.SectionNameConventionTests`. If the test
flags a new top-level section that you intend to keep, edit
`AllowedSingleSegmentSections` with a one-line rationale.

### 3e. Module naming homogeneity (post-rename check)

After a module rename (e.g., `Querying` → `QueryEngine`), ALL artifacts bearing
the module name must be updated consistently. Derive the canonical module name
from the project directory name: `src/Granit.{Module}` → `{Module}`.

**Classes — must embed `{Module}`:**

- [ ] Module class: `Granit{Module}Module` (e.g., `GranitQueryEngineModule`)
- [ ] DbContext: `{Module}DbContext` (e.g., `QueryEngineDbContext`)
- [ ] ModelBuilder extensions: `{Module}ModelBuilderExtensions`
- [ ] ServiceCollection extensions: `{Module}ServiceCollectionExtensions`
- [ ] Options class: `{Module}Options`
- [ ] Metrics class: `{Module}Metrics`
- [ ] ActivitySource class: `{Module}ActivitySource`
- [ ] Permission class: `{Module}Permissions`
- [ ] Permission provider: `{Module}PermissionDefinitionProvider`
- [ ] Localization resource: `{Module}EndpointsLocalizationResource`
- [ ] Health check: `{Module}HealthCheck` (if applicable)

**Methods — must embed `{Module}`:**

- [ ] DI registration: `Add{Module}()`, `AddGranit{Module}()`
- [ ] EF model config: `Configure{Module}Module()`
- [ ] Endpoint mapping: `Map{Module}Endpoints()`

**String literals — must use current module name:**

- [ ] Meter name: `"Granit.{Module}"` (PascalCase)
- [ ] ActivitySource name: `"Granit.{Module}"` (PascalCase)
- [ ] Job name prefix: `"{module-kebab}-"` (kebab-case)
- [ ] Permission group name: `"{Module}"` (PascalCase)
- [ ] Log category / event names

**Namespaces — must match project name:**

- [ ] All `.cs` files in `Granit.{Module}` use `namespace Granit.{Module};`
- [ ] All `.cs` files in `Granit.{Module}.Endpoints` use
  `namespace Granit.{Module}.Endpoints;` (or sub-namespace)
- [ ] All `.cs` files in `Granit.{Module}.EntityFrameworkCore` use
  `namespace Granit.{Module}.EntityFrameworkCore;` (or sub-namespace)
- [ ] Satellite projects follow the same pattern

**Detection strategy:**

1. Extract `{Module}` from the project directory name
2. Grep all `.cs` files in the module family for the **old** module name
   (check recent git renames: `git log --diff-filter=R --name-status -20`)
3. Flag any class, method, string literal, or namespace still using the old name

Ref: `CLAUDE.md §Architecture`, `CLAUDE.md §Package naming convention`

---

## 4. HTTP conventions (`--scope http`)

### 4a. Status codes

- [ ] `TypedResults.Problem()` for all errors (RFC 7807) — never `BadRequest<string>()`
- [ ] `202 Accepted` for async operations — response includes tracking mechanism
  (requestId, Location header, or webhook)
- [ ] `204 No Content` for DELETE, PUT /settings, acknowledgments
- [ ] `207 Multi-Status` for batch operations with mixed results only
- [ ] `409 Conflict` for concurrency conflicts (stale version, duplicate)
- [ ] `422 Unprocessable Entity` for business validation failures (FluentValidation)

Ref: `docs-site/…/architecture/http-conventions.md`

### 4b. URL structure

- [ ] Resource segments in `kebab-case` (`/background-jobs`)
- [ ] Route parameters in `camelCase` (`{patientId}`)
- [ ] Route groups use `MapGranitGroup(prefix)` — not `MapGroup()`

### 4c. Pagination

- [ ] Offset pagination (default): `?page=1&pageSize=20&sort=-createdAt`
- [ ] Cursor pagination (opt-in): `?cursor=<opaque>&pageSize=50`
- [ ] Response via `PagedResult<T>` with `Items`, `TotalCount`, `HasMore`,
  `NextCursor`
- [ ] Sort format: comma-separated, `-` prefix for descending

### 4d. Filtering

- [ ] Field filters: `filter[field.operator]=value`
- [ ] Operators: `eq`, `contains`, `startsWith`, `endsWith`, `gt`, `gte`, `lt`,
  `lte`, `in`, `between`
- [ ] Only fields declared `Sortable()` / filterable in `QueryDefinition`

### 4e. Idempotency

- [ ] `Idempotency-Key` header for POST endpoints (client-generated UUID)
- [ ] Concurrent same-key requests return `409` with `Retry-After`
- [ ] Different payload with same key returns `422`

### 4f. Cache-Control headers

- [ ] User data (`GET /me`, `/settings`): `private, no-cache`
- [ ] Reference data: `public, max-age=3600`
- [ ] Paginated lists: `private, no-store` (GDPR — sensitive data)
- [ ] Static resources: `public, max-age=86400, immutable`
- [ ] **NEVER** `public` for personal data responses (GDPR)

Ref: `docs-site/…/architecture/http-conventions.md`

### 4g. Exception handling middleware

- [ ] `app.UseGranitExceptionHandling()` registered as **first** middleware
- [ ] Custom exception mappers implement `IExceptionStatusCodeMapper`
  (chain of responsibility)
- [ ] `IUserFriendlyException` for user-facing messages
- [ ] `ExposeInternalErrorDetails` is `false` in production (ISO 27001)
- [ ] `IHasErrorCode` for structured error codes in `ProblemDetails`

Ref: `docs-site/…/api/exception-handling.mdx`

---

## 5. OpenAPI endpoint metadata and compatibility (`--scope openapi`)

This scope verifies that every endpoint is correctly declared AND that its runtime
contract (handler signature, return types, parameter binding, authorization)
is consistent with the OpenAPI metadata exposed to clients. Inconsistencies
between the handler and the metadata produce broken SDKs, wrong typings in the
frontend generator, and misleading Scalar documentation.

### 5a. Mandatory metadata elements

Every endpoint MUST declare all 5 elements — chained in this canonical order:

- [ ] `.WithName("VerbNoun")` — PascalCase operation ID
- [ ] `.WithSummary("Imperative sentence.")` — ~100 chars, ends with period
- [ ] `.WithDescription("2-4 sentences...")` — what, context, errors
- [ ] `.Produces<T>()` — success response type (mapped from handler return type)
- [ ] `.ProducesProblem(StatusCodes.StatusXxx)` — one per error path in handler

### 5b. Handler ↔ metadata return-type consistency (CRITICAL)

The handler's declared return type MUST match the `.Produces*()` declarations
exactly — every branch covered, no extra declarations.

- [ ] Handler returns a concrete `Results<...>` discriminated union when it can
  produce multiple status codes — never loose `IResult` or `Task<IResult>`
- [ ] Single-status handlers use the concrete type (`Ok<T>`, `Created<T>`,
  `NoContent`, `Accepted<T>`, `FileStreamHttpResult`, `ProblemHttpResult`)
- [ ] **Every** branch of the `Results<...>` union has a matching
  `.Produces*()` or `.ProducesProblem()` declaration
- [ ] **No** `.Produces*()` declaration without a corresponding handler branch
  (false documentation)
- [ ] No `Task<IActionResult>` or `ActionResult<T>` (MVC pattern — wrong in
  Minimal API)
- [ ] No `async void`; async handlers return `Task<Results<...>>`

### Return type mapping

| Handler return | Produces declaration | ProducesProblem declaration |
| -------------- | ------------------- | --------------------------- |
| `Ok<T>` | `.Produces<T>()` | — |
| `Created<T>` | `.Produces<T>(Status201Created)` | — |
| `Created` (no body) | `.Produces(Status201Created)` | — |
| `NoContent` | `.Produces(Status204NoContent)` | — |
| `Accepted<T>` | `.Produces<T>(Status202Accepted)` | — |
| `Accepted` (no body) | `.Produces(Status202Accepted)` | — |
| `NotFound` | — | `.ProducesProblem(Status404NotFound)` |
| `Conflict<T>` or `Conflict` | — | `.ProducesProblem(Status409Conflict)` |
| `ProblemHttpResult` | — | `.ProducesProblem(StatusXxx)` (one per path) |
| `ValidationProblem` | — | `.ProducesValidationProblem()` |
| `FileStreamHttpResult` | `.Produces(Status200OK, contentType: "…")` | — |
| `PhysicalFileHttpResult` | `.Produces(Status200OK, contentType: "…")` | — |
| `RedirectHttpResult` | `.Produces(Status302Found)` | — |
| `UnauthorizedHttpResult` | — | `.ProducesProblem(Status401Unauthorized)` |

### 5c. Error response coverage

The framework's `ProblemDetailsResponseOperationTransformer`
(`Granit.Http.ApiDocumentation`) **auto-injects** these responses — do NOT
declare them manually (redundant, and the transformer wires the shared
`ProblemDetails` schema under `application/problem+json`):

| Status | Auto-injected when | Manual declaration |
| ------ | ------------------ | ------------------ |
| `401 Unauthorized` | `[Authorize]` present and no `[AllowAnonymous]` | Redundant — do not add |
| `403 Forbidden` | same as 401 | Redundant — do not add |
| `422 Unprocessable Entity` | endpoint has a request body AND no existing 400 | Redundant — do not add |
| `500 Internal Server Error` | always (every operation) | Redundant — do not add |

What the endpoint MUST still declare:

- [ ] Every business error path declared explicitly:
  - `404 Not Found` whenever the handler does a lookup by id and can return
    `TypedResults.NotFound()` — the transformer only **enriches** an existing
    404 with the ProblemDetails schema, it does not create it
  - `409 Conflict` for concurrency conflicts or duplicates
  - `410 Gone`, `412 Precondition Failed`, `423 Locked`, `429 Too Many Requests`
    when the handler returns these explicitly
  - Custom statuses from `TypedResults.Problem(..., statusCode: X)`
- [ ] No `BadRequest<string>()` — always `TypedResults.Problem()` (RFC 7807,
  GRAPI002)
- [ ] Never `Results.*()` — always `TypedResults.*()` (GRAPI001)
- [ ] Do NOT manually declare `.ProducesProblem(401/403/422/500)` — flag
  redundant declarations as `CLEANUP` (noise, drifts from framework defaults)
- [ ] If the endpoint has **no route parameter** and shows 404 in the generated
  OpenAPI document, it's a Wolverine phantom — the transformer strips it
  automatically (no action needed)

### 5d. Route groups and URL structure

- [ ] `endpoints.MapGranitGroup(prefix)` — not `MapGroup()` (enables auto-
  validation and standard filters)
- [ ] Route prefix is kebab-case and plural
- [ ] Route parameters use `camelCase` with explicit constraints where relevant
  (`{id:guid}`, `{tenantId:guid}`, `{version:int}`)
- [ ] Route constraint type matches the parameter type in the handler signature
  (e.g. `{id:guid}` → `Guid id`, `{page:int}` → `int page`)

### 5e. Parameter binding

- [ ] Explicit binding attributes when the source is ambiguous:
  `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, `[FromBody]`, `[FromForm]`,
  `[FromServices]`
- [ ] Complex types bound from query string use `[AsParameters]` with a record
  — not individual `[FromQuery]` parameters (improves OpenAPI schema)
- [ ] `[FromBody]` used at most once per endpoint
- [ ] Cancellation token (`CancellationToken`) is the **last** parameter and is
  not decorated with any binding attribute
- [ ] `HttpContext` parameter is not decorated with a binding attribute
- [ ] Optional parameters have a default value or are nullable — the OpenAPI
  `required` flag must reflect reality

### 5f. Content types — requests

- [ ] JSON endpoints: no explicit `.Accepts<T>()` needed (inferred from
  `[FromBody]`)
- [ ] File upload endpoints (`IFormFile` / `IFormFileCollection`):
  - Handler parameters decorated with `[FromForm]`
  - `.DisableAntiforgery()` applied (Minimal API requires explicit opt-out for
    multipart)
  - `.Accepts<TRequest>("multipart/form-data")` declared so Scalar shows the
    file picker
- [ ] Raw binary body (`Stream`, `PipeReader`): `.Accepts<Stream>("application/
  octet-stream")` or the correct MIME

### 5g. Content types — responses

- [ ] `FileStreamHttpResult` / `PhysicalFileHttpResult` declare the correct
  MIME type via `.Produces(Status200OK, contentType: "application/pdf")` (or
  specific type)
- [ ] Streaming download endpoints default to `application/octet-stream` when
  the content type is unknown at compile time — but set it at runtime on the
  result
- [ ] JSON responses do not redundantly declare `contentType` (inferred)

### 5h. Authorization and anonymous access

- [ ] Every endpoint exposes its security stance explicitly:
  `.RequireAuthorization("Group.Resource.Action")` **or** `.AllowAnonymous()`
  — no implicit default
- [ ] Permission string passed to `RequireAuthorization` exists as a constant
  in the module's `{Module}Permissions` nested static class (grep to verify)
- [ ] Anonymous endpoints are rare and justified (health checks, public docs,
  auth callbacks) — flag unexpected `AllowAnonymous` usage
- [ ] When `.RequireAuthorization(...)` is present, `.ProducesProblem(401)` and
  `.ProducesProblem(403)` are declared (see §5c)

### 5i. Tags and grouping (Scalar UI)

- [ ] `.WithTags(...)` applied on every root `RouteGroupBuilder`. Without it, the
  generator falls back to the handler's declaring class name (e.g.
  `AccountLoginEndpoints`) — a BREAKING OpenAPI output issue, not a style nit
- [ ] Tag value format: **Title Case with spaces** — `"Blob Storage"`,
  `"Background Jobs"`, `"Reference Data"`. NEVER glued PascalCase
  (`"BlobStorage"`, `"MobilePush"`, `"CustomerBalance"`), NEVER kebab/snake_case
- [ ] Multi-tag modules use `"<Module> - <SubGroup>"` (space-dash-space) so sub-tags
  group visually in Scalar — e.g. `"AI - Workspaces"`, `"Identity - User Cache"`,
  `"Notifications - Mobile Push"`. Pattern enforced across `Granit.AI.Endpoints`,
  `Granit.Identity.Endpoints`, `Granit.Notifications.Endpoints`,
  `Granit.MultiTenancy.Endpoints`
- [ ] Single-tag modules use a natural user-facing name (module namespace does not
  dictate the tag — e.g. `Granit.Authentication.ApiKeys.Endpoints` → `"API Keys"`)
- [ ] Tag exposed via `TagName` (or `{Role}TagName` for multi-tag) on the
  module's `*EndpointsOptions` so consumers can override
- [ ] Tag list is sorted alphabetically in the generated document — verified by
  `SortedTagsDocumentTransformer` (registered in `Granit.Http.ApiDocumentation`).
  No per-app action needed; flag only if the transformer is missing from
  `ApiDocumentationServiceCollectionExtensions`

### 5j. OperationId uniqueness

- [ ] `.WithName(...)` values are unique across the **entire solution** (Scalar
  uses them as anchors; SDK generators use them as method names)
- [ ] No duplicates across modules (common collision: `Create`, `Delete`,
  `GetById` without a noun prefix)
- [ ] Use specific verbs: `CreateBlobDescriptor`, not `Create`

### 5k. Schema examples and DTOs

- [ ] `ISchemaExampleProvider<TRequest>` implemented for each non-trivial
  `*Request` DTO with realistic values (no `"string"`, `0`, `00000000-0000-...`)
- [ ] Enum JSON serialization uses `JsonStringEnumConverter` (PascalCase) —
  never raw integers; an opaque `type: integer` enum is useless to TypeScript
  codegen and to anyone reading the payload
- [ ] **Polymorphic DTOs declare their hierarchy explicitly** — any `*Request`/
  `*Response` base type with derived wire types carries
  `[JsonDerivedType(typeof(Derived), "discriminatorValue")]` (one per derived
  type) so the OpenAPI document emits a `discriminator` mapping. Without it,
  TypeScript generators (openapi-ts, Orval) silently degrade the union to
  `any` or an unusable intersection — flag as BREAKING, not style
- [ ] Date/time properties are `DateTime` / `DateTimeOffset` → ISO 8601 with
  timezone in OpenAPI
- [ ] Identifiers are `Guid` → `string` with `format: uuid` in OpenAPI
- [ ] Value objects (`SingleValueObject<T>`) expose the underlying primitive in
  OpenAPI via the framework's `SingleValueObjectSchemaTransformer`
- [ ] Request DTOs declared in `.Endpoints/Dtos/` — entities are never returned

### 5l. Deprecation and versioning

- [ ] Deprecated endpoints use
  `.WithMetadata(new DeprecatedAttribute { SunsetDate = "YYYY-MM-DD", Link = "…" })`
  from `Granit.Http.ApiVersioning.Deprecation` — emits RFC 8594 `Deprecation`,
  `Sunset`, and `Link` response headers (preferred over raw
  `op.Deprecated = true`)
- [ ] Add `[Obsolete]` on the handler method so callers see a compiler warning
- [ ] No hand-rolled API versioning in URLs (`/v1/…`) — versioning is driven by
  `ApiDocumentation:MajorVersions` in configuration (generates
  `/openapi/v1.json`, `/openapi/v2.json`)
- [ ] One OpenAPI document per declared major version — registered by
  `Granit.Http.ApiDocumentation` (never call `AddOpenApi()` manually in app code
  when the module is used)

### 5m. Internal / hidden endpoints

- [ ] Internal-only endpoints (inter-service webhooks, sync jobs, raw admin)
  marked with `[InternalApi]` from `Granit.Http.ApiDocumentation.Attributes`
  — silently excluded from all generated OpenAPI documents by
  `InternalApiDocumentTransformer`
- [ ] Do NOT use `[ExcludeFromDescription]` — the framework's `[InternalApi]`
  is the idiomatic marker and integrates with the document transformer
  pipeline

### 5n. Granit framework alignment (`Granit.Http.ApiDocumentation`)

- [ ] Native `Microsoft.AspNetCore.OpenApi` via `GranitHttpApiDocumentationModule`
  only — NEVER Swashbuckle or NSwag (flag any `Swashbuckle.*` or `NSwag.*`
  `<PackageReference>`)
- [ ] Host calls `app.UseGranitApiDocumentation()` in `Program.cs` (not manual
  `MapOpenApi()` + custom UI wiring)
- [ ] `ApiDocumentationOptions` bound from `ApiDocumentation` section in
  `appsettings.json` (`MajorVersions`, `Title`, `Description`, `PartyEmail`,
  `LogoUrl`, `FaviconUrl`, `EnableInProduction`, `EnableTenantHeader`,
  `AuthorizationPolicy`, `OAuth2`)
- [ ] `EnableInProduction = false` unless explicitly required (ISO 27001 —
  OpenAPI surface is a reconnaissance vector)
- [ ] Tenant-aware apps: `EnableTenantHeader = true` documents the
  `X-Tenant-Id` header on every endpoint; anonymous-tenant endpoints opt out
  with `[AllowAnonymousTenant]`
- [ ] OAuth2 flows use `OAuth2Options` binding — do not hand-wire
  `OpenApiSecurityScheme` (the `OAuth2SecuritySchemeTransformer` handles it)
- [ ] Request DTO examples supplied via `ISchemaExampleProvider` implemented
  in each `.Endpoints` package — NEVER inline JSON in the endpoint chain,
  NEVER `[OpenApiExample]`-style hacks. The
  `SchemaExampleSchemaTransformer` deep-clones examples from the provider.
- [ ] Well-known parameter names (`userId`, `roleName`, `permissionName`,
  `entityType`, `entryId`, `jobId`, etc.) inherit descriptions from
  `ParameterDescriptionOperationTransformer` — do NOT re-describe them
  manually unless overriding

### 5o. Auto-injected responses — do NOT duplicate

`ProblemDetailsResponseOperationTransformer` injects responses based on
endpoint metadata. Manual declarations for these statuses are redundant
noise and should be flagged as `CLEANUP`:

- [ ] No manual `.ProducesProblem(StatusCodes.Status401Unauthorized)` on
  `[Authorize]` endpoints — auto-injected
- [ ] No manual `.ProducesProblem(StatusCodes.Status403Forbidden)` on
  `[Authorize]` endpoints — auto-injected
- [ ] No manual `.ProducesProblem(StatusCodes.Status422UnprocessableEntity)`
  on endpoints with a request body — auto-injected when no 400 exists
- [ ] No manual `.ProducesProblem(StatusCodes.Status500InternalServerError)`
  — auto-injected on every operation
- [ ] 404 response from `TypedResults.NotFound()` is automatically enriched
  with the shared `ProblemDetails` schema — the endpoint only needs to
  declare `.ProducesProblem(404)` if the handler branches to it

### 5p. Detection strategy

When auditing a module's endpoints:

1. Enumerate endpoint files: `Glob src/Granit.{Module}.Endpoints/**/*Endpoints.cs`
2. For each file, use MCP `get_file_overview` to list `Map*` methods
3. For each `Map*` method, use MCP `get_symbol_detail` to read the chain
4. Cross-reference the handler signature (return type, parameters) with the
   `.With*`/`.Produces*` chain — mismatches are `BREAKING` (wrong SDK output)
5. Grep for violations:
   - `Results\.(Ok|BadRequest|NotFound|Problem)` → use `TypedResults` (GRAPI001)
   - `BadRequest<string>` → use `Problem()` (GRAPI002)
   - `Swashbuckle|NSwag` in `.csproj` → remove
   - `MapGroup\(` (without `Granit`) in `.Endpoints/` → use `MapGranitGroup`
   - `.RequireAuthorization\(\)` with no argument → specify a permission string
   - `Task<IActionResult>` or `ActionResult<` → wrong pattern in Minimal API

### Severity guidance

| Finding | Severity |
| ------- | -------- |
| Handler branch not declared in `.Produces*()` | BREAKING (SDK mismatch) |
| `.Produces*()` without matching handler branch | CONVENTION |
| Missing one of the 5 mandatory metadata elements | CONVENTION |
| Duplicate `OperationId` across solution | BREAKING (SDK codegen fails) |
| Missing `.RequireAuthorization` / `.AllowAnonymous` | ARCHITECTURE (security) |
| `MapGroup` instead of `MapGranitGroup` | CONVENTION (loses auto-validation) |
| Entity returned from endpoint | ARCHITECTURE (leaks persistence) |
| Swashbuckle / NSwag reference | ARCHITECTURE |
| Wrong route constraint type | BREAKING (binding fails at runtime) |
| Missing `.DisableAntiforgery()` on multipart | BREAKING (endpoint rejects requests) |
| Polymorphic DTO base without `[JsonDerivedType]` discriminator | BREAKING (TS codegen emits `any`) |
| Enum exposed as raw integer in a wire DTO | CONVENTION |

Ref: `CLAUDE.md §OpenAPI metadata`, `docs-site/…/api/api-documentation.mdx`,
`docs-site/…/architecture/http-conventions.md`

---

## 6. Persistence (`--scope persistence`)

### 6a. Isolated DbContext

- [ ] `<ProjectReference>` to `Granit.Persistence`
- [ ] Constructor injects `ICurrentTenant?` and `IDataFilter?` (both optional,
  default `null`)
- [ ] `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` called
  **last** in `OnModelCreating`
- [ ] Registration via `AddGranitDbContext<T>(...)` — not manual `AddDbContext<T>()`
- [ ] Interceptors wired via `(sp, options)` overload (resolve
  `AuditedEntityInterceptor`, `SoftDeleteInterceptor`)
- [ ] `[DependsOn(typeof(GranitPersistenceModule))]` on module class
- [ ] No manual `HasQueryFilter` — `ApplyGranitConventions` handles all filters
- [ ] Named query filters via `HasQueryFilter(name, expr)` if custom filters needed

Ref: `docs-site/…/data/persistence.mdx`, `docs-site/…/data/query-filters.mdx`

### 6b. Entity configuration

- [ ] `IMultiTenant` entities use `Guid? TenantId` (nullable — never non-nullable
  `Guid`, never `string`)
- [ ] Entity type configurations in `EntityTypeConfiguration/` folder
- [ ] Each configuration in its own file
- [ ] **Enum persistence**: no manual `.HasConversion<string>()` on enum
  properties — `ApplyGranitConventions` already persists every enum as its
  PascalCase string name in a `varchar` column (CLAUDE.md §Enum persistence)
- [ ] `[PersistAsInt]` opt-outs are justified inline — reserved for `[Flags]`
  bitmasks (auto-skipped anyway), high-write tables, or pre-existing DB
  contracts; an undocumented `[PersistAsInt]` is a CONVENTION finding
- [ ] Int→string enum migrations in consuming apps use
  `MigrationBuilderExtensions.AlterEnumColumnIntToString<TEnum>` (emits the
  mandatory PostgreSQL `USING CASE` clause)

### 6c. Interceptor pipeline awareness

Five interceptors execute in strict order:

1. `AuditedEntityInterceptor` — `CreatedAt/By`, `ModifiedAt/By`, auto-Id, TenantId
2. `VersioningInterceptor` — `VersionId`, `Version` on `IVersioned`
3. `ConcurrencyStampInterceptor` — regenerates `ConcurrencyStamp` on `IConcurrencyAware`
4. `DomainEventDispatcherInterceptor` — collects/dispatches domain events
5. `SoftDeleteInterceptor` — converts DELETE to UPDATE for `ISoftDeletable`

Critical bypass rules:

- [ ] `ExecuteUpdate()` bypasses ALL interceptors — must set audit fields, concurrency
  stamp, and soft-delete flag explicitly
- [ ] `ExecuteDelete()` bypasses `SoftDeleteInterceptor` — use `ExecuteUpdate()` with
  explicit `IsDeleted = true` or `DbContext.Remove()`
- [ ] For disconnected CQRS updates: explicitly set
  `db.Entry(entity).Property(e => e.ConcurrencyStamp).OriginalValue`

Ref: `docs-site/…/data/interceptors.mdx`

### 6d. Concurrency

- [ ] `IConcurrencyAware` entities have `ConcurrencyStamp` property
- [ ] Response DTOs include `ConcurrencyStamp`
- [ ] Request DTOs accept stamp back (implement `IConcurrencyStampRequest`)
- [ ] `DbUpdateConcurrencyException` mapped to HTTP 409 via
  `EfCoreExceptionStatusCodeMapper`
- [ ] Tests use SQLite or Testcontainers — never `UseInMemoryDatabase()` for
  concurrency (ignores tokens → false positive)

**Detection strategy — `ConcurrencyStamp` gap in response DTOs:**

1. Find all `IConcurrencyAware` entities in the module:

   ```bash
   grep -rl "IConcurrencyAware" src/Granit.{Module}/ --include="*.cs"
   ```

2. For each entity, confirm at least one **mutation** endpoint exists (PUT, PATCH,
   or a POST that updates state) in the matching `.Endpoints` project.
3. Locate the corresponding `*Response` DTO (usually `{EntityName}Response.cs` in
   `src/Granit.{Module}.Endpoints/Dtos/`).
4. Check whether `ConcurrencyStamp` (type `string`, non-null) is among the record
   parameters. If absent while the entity implements `IConcurrencyAware` AND a
   mutation endpoint exists → **IMPROVEMENT** finding.
   - Severity bumps to **CONVENTION** when a round-trip conflict path (409) is
     already declared on the endpoint but the stamp is not exposed — the client
     cannot build the ETag without it.
5. Also check the matching `*Request` DTO for the mutation endpoint: it should accept
   the stamp back via `IConcurrencyStampRequest` (separate story — flag as
   **IMPROVEMENT** if missing but do not block on it).

Reference implementation: `ImportJobResponse` / `ExportJobResponse` in
`Granit.DataExchange.Endpoints`.

Ref: `docs-site/…/data/concurrency.mdx`

### 6f. Response DTO completeness — audit fields

Every `*Response` DTO must expose the audit fields that its domain entity inherits
from the base class, so that clients can display provenance and sort/filter
without a second request.

**Rules by base class:**

| Entity base | Required in `*Response` | Optional |
| ----------- | ----------------------- | -------- |
| `AuditedEntity` / `AuditedAggregateRoot` | `createdAt` (non-null), `modifiedAt` (nullable) | `createdBy`, `modifiedBy` |
| `FullAuditedEntity` / `FullAuditedAggregateRoot` | `createdAt`, `modifiedAt` (nullable) | `createdBy`, `modifiedBy`, `deletedAt`, `deletedBy` |
| `CreationAuditedEntity` / `CreationAuditedAggregateRoot` | `createdAt` (non-null) | `createdBy` — **do NOT add `modifiedAt`** (not provided by base) |
| `Entity` | — | nothing |

`modifiedAt` is always **nullable** (`DateTimeOffset?`) — `null` until the first
mutation occurs. Never use `updatedAt` (convention: `modifiedAt`).

**Detection strategy:**

1. For each domain entity in `src/Granit.{Module}/`, inspect its base class:

   ```bash
   grep -rn "AuditedAggregateRoot\|FullAuditedAggregateRoot\|AuditedEntity\|FullAuditedEntity" \
     src/Granit.{Module}/ --include="*.cs"
   ```

2. Locate the corresponding `*Response` DTO in `src/Granit.{Module}.Endpoints/Dtos/`.

3. Check whether `ModifiedAt` (or `modifiedAt`) appears in the record parameter list.
   Absence on an `Audited*` entity → **IMPROVEMENT** finding (bumps to **CONVENTION**
   if the entity has a mutation endpoint, because the client cannot know the
   last-update time without polling).

4. `CreationAudited*` entities that correctly expose only `CreatedAt` are **NOT** a
   finding — do not flag the absence of `ModifiedAt` on these.

Reference implementations: `RoleResponse` (with `ModifiedAt`),
`NotificationSubscriptionResponse` (correctly `CreatedAt`-only,
`CreationAuditedEntity`), `ImportJobResponse` (full set).

Ref: `CLAUDE.md §DTOs`, `docs-site/…/data/persistence.mdx`

### 6e. Migrations

- [ ] Migration files in `Migrations/` folder
- [ ] No data seeding in migrations (use `IDataSeedContributor`)
- [ ] `DropColumn` requires Contract-phase annotation (GRMIGA001)
- [ ] `RenameColumn` not zero-downtime safe (GRMIGA002)
- [ ] `AddColumn NOT NULL` without default risks table lock (GRMIGA003)
- [ ] `AlterColumn` type change requires Contract-phase annotation (GRMIGA004)
- [ ] Column renames, type changes, splits use Expand & Contract pattern

Ref: `docs-site/…/data/migrations.mdx`, `docs-site/…/core/analyzers.mdx`

---

## 7. DDD (`--scope ddd`)

### 7a. Aggregate roots

- [ ] Inherits `AggregateRoot` or audited variant (`FullAuditedAggregateRoot`, etc.)
- [ ] All properties: `{ get; private set; }`
- [ ] Factory method: `public static Xxx Create(...)` — only construction path
- [ ] Private EF Core constructor: `private Xxx() { }` — required for materialization
- [ ] Behavior methods for state transitions (`MarkAsValid()`, `Revoke()`)
- [ ] Domain events via `AddDomainEvent()` / `AddDistributedEvent()` — never manual
  `IDomainEventSource` implementation
- [ ] Explicit `IMultiTenant` interface if `TenantId` has `private set`:
  `Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }`
- [ ] No public setters (enforced by architecture tests)

Ref: `docs-site/…/architecture/adr/017-ddd-aggregate-value-object-strategy.md`

### 7b. Classification criteria (ADR-017)

Use `AggregateRoot` when:

- Entity has state machine (status transitions with business rules)
- Raises domain or integration events
- Encapsulates invariants

Use plain `Entity` (anemic, legitimate) when:

- Append-only / immutable after creation (audit logs, consent records)
- Configuration record (settings, feature flags)
- Cache/mirror of external system
- Lookup table (reference data)

- [ ] Entity classification matches ADR-017 criteria
- [ ] `AggregateRoot` NOT used for simple CRUD entities without invariants

### 7c. Value objects

- [ ] Inherits `SingleValueObject<T>` for single-primitive wrappers
- [ ] `sealed` with `init` properties
- [ ] `Create()` factory with validation
- [ ] Implicit operators for backward compat
- [ ] EF Core converters auto-applied by `ApplyGranitConventions`
- [ ] JSON handled by `SingleValueObjectJsonConverterFactory`

### 7d. Entity hierarchy

Verify correct base class selection:

| Base class | When |
| ---------- | ---- |
| `Entity` | No audit fields needed |
| `CreationAuditedEntity` | Created tracking only |
| `AuditedEntity` | Created + Modified tracking |
| `FullAuditedEntity` | + Soft delete |
| `AggregateRoot` | Domain events, no audit |
| `CreationAuditedAggregateRoot` | Events + Created |
| `AuditedAggregateRoot` | Events + Created + Modified |
| `FullAuditedAggregateRoot` | Events + Created + Modified + Soft delete |

---

## 8. Validation (`--scope validation`)

### 8a. Validator conventions

- [ ] Every `*Request` type has a corresponding `AbstractValidator<T>`
- [ ] Validators are auto-discovered by `GranitValidationModule` (no manual
  registration)
- [ ] Route groups use `MapGranitGroup(prefix)` for auto-validation
- [ ] Opt-out via `SkipAutoValidationAttribute` only when justified

### 8b. Localized messages

- [ ] No hardcoded `.WithMessage("...")` strings
- [ ] Built-in validators use auto-converted error codes
  (`GranitErrorCodeLanguageManager`)
- [ ] Custom `.Must()` validators use
  `.WithErrorCodeAndMessage("Granit:Validation:XxxCode")`
- [ ] Error code present in all 18 JSON files in
  `src/Granit.Validation/Localization/Validation/` (15 base + fr-CA, en-GB,
  pt-BR)

Ref: `CLAUDE.md §Validation`

---

## 9. Events (`--scope events`)

### 9a. Domain events

- [ ] Implement `IDomainEvent`
- [ ] `*Event` suffix (past-tense verb + `Event`)
- [ ] Raised via `AddDomainEvent()` — synchronous, same transaction
- [ ] Handlers run after commit (`SavedChanges`)

### 9b. Integration events

- [ ] Implement `IIntegrationEvent`
- [ ] `*Eto` suffix (past-tense verb + `Eto`)
- [ ] Raised via `AddDistributedEvent()` — durable, Wolverine outbox
- [ ] Persisted atomically before commit (`SavingChanges`)
- [ ] ETO design: flat, serializable (primitives, Guids, value types only) —
  no lazy-loaded navigations or DI services
- [ ] ETO is self-contained snapshot — stable across service boundaries

### 9c. Generic lifecycle events

- [ ] `IEmitEntityLifecycleEvents` marker for local domain events only
- [ ] `IHasEntityEto<TEto>` for local + distributed events
- [ ] `ToEto()` method returns flat serializable snapshot
- [ ] Soft delete (`IsDeleted` transition) dispatches `EntityDeletedEvent<T>` —
  not update event

### 9d. Event infrastructure

- [ ] For distributed events: `[DependsOn(typeof(GranitEventsWolverineModule))]`
- [ ] Without Wolverine: ETOs silently dropped (intentional — no overhead)

Ref: `docs-site/…/data/entity-lifecycle-events.mdx`

---

## 10. Metrics and diagnostics (`--scope metrics`)

### 10a. Metrics class

- [ ] `sealed class {Module}Metrics` in `Diagnostics/` folder
- [ ] Constructor injects `IMeterFactory`
- [ ] Meter name: `"Granit.{Module}"` (PascalCase)
- [ ] Metric names: `granit.{module}.{entity}.{action}` (all lowercase, dot-separated)
- [ ] Tags: `snake_case`, always include `tenant_id` (coalesced to `"global"`)
- [ ] Tags passed via `TagList`
- [ ] DI: `services.TryAddSingleton<{Module}Metrics>();`

### 10b. Activity source

- [ ] `internal static class {Module}ActivitySource` in `Diagnostics/` folder
- [ ] Registered via `GranitActivitySourceRegistry.Register(Name)` in `Add*()` extension
- [ ] One `ActivitySource` per module

### 10c. Health checks

- [ ] Custom health checks tagged `"readiness"` and/or `"startup"` for probe discovery
- [ ] Defensive 10-second timeout: `.WaitAsync(10s, cancellationToken)`
- [ ] **No PII, secrets, connection strings, or stack traces** in health check
  responses (ISO 27001 / GDPR)
- [ ] Health check registration via `AddGranit*HealthCheck()` pattern
- [ ] `CachedHealthCheck` wrapper for stampede protection (default 10s TTL)

### 10d. Health check paths

| Probe | Path | Behavior |
| ----- | ---- | -------- |
| Liveness | `/health/live` | Always 200, no dependency checks |
| Readiness | `/health/ready` | Checks tagged `"readiness"` |
| Startup | `/health/startup` | Checks tagged `"startup"` |

Ref: `CLAUDE.md §Metrics`, `docs-site/…/core/diagnostics.mdx`

---

## 11. Localization (`--scope localization`)

### 11a. Culture completeness

18 cultures required: `en`, `fr`, `nl`, `de`, `es`, `it`, `pt`, `zh`, `ja`,
`pl`, `tr`, `ko`, `sv`, `cs`, `hi`, `fr-CA`, `en-GB`, `pt-BR`

- [ ] All `src/*/Localization/**/*.json` files exist for all 18 cultures
- [ ] Regional files (`fr-CA`, `en-GB`, `pt-BR`) contain only differing keys
- [ ] Base culture files contain all keys

### 11b. Localization resources

- [ ] `internal sealed class {Module}EndpointsLocalizationResource` with
  `[LocalizationResourceName]` attribute (if module has endpoints)
- [ ] Permission localization keys present:
  `PermissionGroup:{Group}`, `Permission:{Group}.{Resource}.{Action}`
- [ ] Validation error codes present in
  `src/Granit.Validation/Localization/Validation/` for custom validators

Ref: `CLAUDE.md §Localization`

---

## 12. Dependencies (`--scope deps`)

### 12a. Project references

- [ ] No circular references between projects
- [ ] `[DependsOn]` on module class matches `<ProjectReference>` with `*Module` types
- [ ] Transitive dependencies not duplicated in `[DependsOn]`
- [ ] Alphabetical order in `[DependsOn]` attributes

### 12b. Package references

- [ ] No duplicate `<PackageReference>` entries
- [ ] Version managed via `Directory.Packages.props` (central package management)
- [ ] No pinned versions — use `Version` from central management

### 12c. Third-party notices

- [ ] `THIRD-PARTY-NOTICES.md` updated for any new external dependency
- [ ] No GPL/LGPL/AGPL/SSPL licensed packages (flag immediately)
- [ ] Summary table and `Derniere mise a jour` date current

### 12d. Multi-tenancy soft dependency

- [ ] `using Granit.MultiTenancy;` — not a hard reference to
  `Granit.MultiTenancy`
- [ ] No `[DependsOn(GranitMultiTenancyModule)]` unless strict tenant isolation
  is required (GDPR)
- [ ] `IsAvailable` checked before using `ICurrentTenant.Id`

Ref: `docs-site/…/concepts/multi-tenancy.mdx`

### 12e. Provider SDK confinement (cloud agnosticism / vendor lock-in)

Granit must stay deployable on any infrastructure (sovereign EU clouds, on-prem
K8s) — vendor SDKs are confined to `.{Provider}` packages and must never leak
into the base module, `.Endpoints`, or `.EntityFrameworkCore`:

- [ ] No `<PackageReference>` to a vendor SDK (`AWSSDK.*`, `Azure.*`,
  `Google.Cloud.*`, `Amazon.*`, `Twilio*`, `SendGrid*`, vendor `*.Sdk`)
  outside `src/Granit.{Module}.{Provider}/` projects
- [ ] No vendor type (`AmazonS3Client`, `BlobServiceClient`, …) in the public
  API or method signatures of the base module — abstractions speak in framework
  types and primitives only
- [ ] No vendor-specific configuration key (`AccessKeyId`, `ConnectionString`
  with vendor scheme, region names) in base-module `*Options` — provider
  options live in the provider package with their own `SectionName`
  (`{Module}:{Provider}` or `Notifications:{Channel}:{Provider}`)
- [ ] The base module compiles and runs with zero provider packages installed
  (provider chosen by the host via DI)

**How to detect:**

```bash
# Vendor SDK leaking outside provider packages
grep -lE 'AWSSDK|Azure\.|Google\.Cloud|Amazon\.' src/Granit.*/[!.]*.csproj \
  | grep -vE 'Granit\.[^.]+\.(S3|AzureBlob|GoogleCloud|Aws|Azure|Cognito|EntraId|Keycloak|AwsSes|AwsSns|AzureCommunicationServices|AzureNotificationHubs|AzureOpenAI)'
```

Severity: ARCHITECTURE (a vendor type in an abstraction welds every consumer
to that vendor).

---

## 13. Compliance (`--scope compliance`)

### 13a. GDPR

- [ ] `ISoftDeletable` for logical deletion (Art. 17 right to erasure)
  — `IsDeleted`, `DeletedAt`, `DeletedBy` fields
- [ ] 3-year minimum retention before physical purge (ISO 27001)
- [ ] `IProcessingRestrictable` for Art. 18 processing restriction
- [ ] `IPersonalDataProvider` implemented for data portability export
- [ ] No PII in logs — never log user data, email, names
- [ ] `private, no-store` Cache-Control on personal data endpoints
- [ ] User cache entries hard-deleted on GDPR erasure (not soft-deleted)

Ref: `docs-site/…/concepts/compliance.mdx`

### 13b. ISO 27001

- [ ] `AuditedEntity` / `AuditedAggregateRoot` for audit trail on all business data
- [ ] Audit fields populated by interceptor — never manual `CreatedAt = ...`
- [ ] Encryption at rest via `IStringEncryptionService` (Vault in production,
  AES in dev/test)
- [ ] HTTPS-only enforced (`RequireHttpsMetadata = true` on JWT Bearer)
- [ ] `ExposeInternalErrorDetails = false` in production
- [ ] `AlwaysAllow = false` in authorization (validator rejects outside Development)
- [ ] Timeline module for entity-level activity log (if applicable)

### 13c. Roslyn analyzers

All analyzer violations must be resolved (not suppressed without justification):

| Rule | Severity | What |
| ---- | -------- | ---- |
| GRMOD001 | Error | Cross-module internal reference |
| GRMIGA001 | Error | `DropColumn` needs Contract-phase annotation |
| GRMIGA002 | Error | `RenameColumn` not zero-downtime safe |
| GRMIGA003 | Warning | `AddColumn NOT NULL` without default |
| GRMIGA004 | Warning | `AlterColumn` type change needs annotation |
| GRSEC001 | Warning | `DateTime.Now/UtcNow` — use `IClock` |
| GRSEC002 | Warning | `Guid.NewGuid()` — use `IGuidGenerator` |
| GRSEC003 | Error | Hardcoded secret detected |
| GRSEC004 | Warning | Direct `IResponseCookies` — use `IGranitCookieManager` |
| GREF001 | Warning | `SaveChanges()` — use `SaveChangesAsync()` |
| GRAPI001 | Warning | `Results` — use `TypedResults` |
| GRAPI002 | Warning | `BadRequest` — use `Problem()` (RFC 7807) |

- [ ] No `#pragma warning disable` for GRMOD/GRSEC rules without justification
- [ ] Architecture tests (NetArchTest at CI) not bypassable by suppressing analyzer

Ref: `docs-site/…/core/analyzers.mdx`

### 13d. Security baseline

- [ ] No hardcoded secrets (not even in comments or examples)
- [ ] No PII in logs
- [ ] No plaintext secret storage
- [ ] No `new HttpClient()` — use `IHttpClientFactory`
- [ ] Authentication PostConfigure pattern for provider modules (Keycloak, EntraId,
  Cognito) — modifies default JWT Bearer scheme in place

Ref: `CLAUDE.md §Security`, `docs-site/…/concepts/security-model.mdx`

---

## 14. Documentation (`--scope docs`)

### 14a. Module documentation page

Every module with public API surface MUST have a documentation page in
`../granit-docs/src/content/docs/dotnet/` (sibling repo). Doc fixes ship in a
**separate PR against granit-docs** — in `--fix` mode, report the finding with
a self-contained handoff prompt instead of editing cross-repo. The page lives
under the appropriate domain subdirectory:

| Domain | Directory | Example modules |
| ------ | --------- | --------------- |
| Core | `core/` | Validation, Observability, Diagnostics |
| Data | `data/` | Persistence, BlobStorage, Caching, Encryption |
| API | `api/` | Http, RateLimiting, Webhooks |
| Security | `security/` | Authentication, Authorization, Identity |
| Infrastructure | `infrastructure/` | BackgroundJobs, Notifications, Localization |
| Business | `business/` | Workflow, QueryEngine, Templating, DataExchange |
| Compliance | `compliance/` | Auditing, Privacy |
| AI | `ai/` | AI modules |

- [ ] Documentation page exists for the module (`{module-kebab}.mdx`)
- [ ] File name matches current module name (not an old name after rename)
- [ ] Frontmatter `title` uses the current module name
- [ ] Frontmatter `description` is accurate and current

### 14b. Content consistency with code

- [ ] Package/namespace references use the current module name
  (e.g., `Granit.QueryEngine`, not `Granit.Querying`)
- [ ] `using` statements in code samples match current namespaces
- [ ] DI registration examples use current method names
  (`AddQueryEngine()`, not `AddQuerying()`)
- [ ] Class names in code samples match current class names
- [ ] Interface names referenced are current and exist in the codebase
- [ ] Configuration keys (`appsettings.json` examples) match current `SectionName`

### 14c. Code samples

- [ ] Code samples compile (use current API surface — verify with
  `get_public_api` MCP)
- [ ] Code samples follow CLAUDE.md conventions (primary constructors,
  collection expressions, `TypedResults`, etc.)
- [ ] No deprecated patterns in samples (check anti-patterns list)

### 14d. Cross-references and links

- [ ] Internal links (`/dotnet/data/persistence/`) point to existing pages
- [ ] "See also" or related module links are present where relevant
- [ ] Links to architecture patterns reference the correct pattern page

### 14e. Counters

- [ ] `PACKAGE_COUNT` in `../granit-docs/src/data/constants.ts` matches
  actual count: `ls src/ | grep "^Granit\." | wc -l`
- [ ] `PATTERN_COUNT` and `ADR_COUNT` are current (check only in `all` mode)

### 14f. Post-rename documentation sweep

After a module rename, verify:

- [ ] Old documentation file renamed or redirected (e.g., `querying.mdx` →
  `query-engine.mdx`)
- [ ] All other `.mdx` files that reference the old module name are updated
  (grep `docs-site/` for old name)
- [ ] Sidebar/navigation config updated if applicable
- [ ] Architecture diagrams (`*.md` with Mermaid) use current module name

**Detection strategy:**

1. Check recent renames: `git log --diff-filter=R --name-status -20 -- 'src/'`
2. For each renamed module, grep docs for old name:
   `grep -r "OldModuleName" ../granit-docs/src/content/`
3. Flag any occurrence as CONVENTION severity

Ref: `CLAUDE.md §Documentation site`

---

## 15. Microservices & Kubernetes compatibility (`--scope microservices`)

A Granit module is a library, but it is consumed by services that run as **multiple
replicas in a Kubernetes cluster**, behind an ingress, with secrets from Vault and
config from the environment. This category is a **deep, cross-cutting analysis** of
whether a module behaves correctly when (a) more than one instance runs at once,
(b) any instance can be killed (SIGTERM) at any moment, (c) external dependencies
(DB, bus, blob store, other services) are reachable only over the network, and
(d) nothing may be assumed about the local filesystem or host.

> This is the most analytical scope. Do not stop at "the checkbox is checked" —
> reason about runtime behavior under N replicas and pod churn. Flag the _concrete
> failure scenario_ (e.g. "recurring job fires on every replica → N× duplicate
> emails"), not just the missing pattern. Read git history before flagging — many
> single-instance shortcuts are deliberate for a reason documented in a prior fix.

### 15a. Statelessness & horizontal scalability

The cardinal rule: **any instance must be able to serve any request, and N
instances must produce the same result as 1.**

- [ ] No mutable `static` / singleton field that accumulates per-request or
  per-tenant state (in-memory counters, dictionaries, queues used as work buffers)
- [ ] No in-process-only cache treated as a source of truth — `IMemoryCache` is a
  per-pod optimization only; cross-replica consistency needs FusionCache **with a
  backplane** (Redis) or a distributed cache
- [ ] No `AsyncLocal`/`ThreadLocal` state that can leak across pooled threads
  between requests — explicitly verify `ICurrentTenant` resolution does not bleed
  across replicas/requests (known leak: see [[project_current_tenant_asynclocal_leak]],
  repro `MultiTenantFilterParameterizationReproTests`)
- [ ] No in-memory session / sticky-session assumption — auth/BFF state must be in
  a shared store (distributed cache / signed cookie), not pod memory
- [ ] No reliance on a process-wide lock (`lock`, `SemaphoreSlim`) to serialize
  work that must be serialized **cluster-wide** — needs a distributed lock
- [ ] Singletons holding a connection/channel (bus, DB, blob client) are
  replica-safe and reconnect after a network blip

**How to detect:** `grep` for `static .*(Dictionary|List|Queue|HashSet|=\s*new)`
in non-test code; `MCP detect_antipatterns`; inspect `IMemoryCache` usages and ask
"what breaks if replica B doesn't have this entry?".

### 15b. Background jobs, schedulers & singleton work (CRITICAL)

The most common multi-replica bug: work that must run **once** runs **per replica**.

- [ ] Recurring jobs (`[RecurringJob]` / `IBackgroundJob`) rely on the distributed
  scheduler so a cron fires **once cluster-wide**, not once per pod — never an
  in-process `Timer`/`PeriodicTimer`/`BackgroundService` loop for scheduled work
- [ ] Any "run on startup / on leader only" work uses leader election or a
  distributed lock, not "every replica does it"
- [ ] Job handlers are **idempotent** — safe if the scheduler double-fires after a
  pod restart (at-least-once)
- [ ] Long-running jobs honor `CancellationToken` so they abort on SIGTERM

**Failure scenario to write up:** "3 replicas × `privacy-purge` cron ⇒ purge runs
3× concurrently ⇒ race / triple audit entries." Severity ARCHITECTURE.

### 15c. Inter-module communication: contracts, messaging, idempotency

In a modular monolith a direct method call into another module's class is the
tempting shortcut — but it is exactly the strong coupling + in-process latency that
**blocks** a module from ever becoming a standalone service. Audit the boundary as
if the call already crossed the network.

**Strict contracts — never expose internal domain classes across a module boundary:**

- [ ] A module is consumed only through its **`Granit.{Module}.Abstractions`
  contracts** (interfaces, `*Request`/`*Response`, `*Eto`/`*Event` records) — never
  by referencing its `Granit.{Module}` domain/EF entities, `AggregateRoot`s, or
  `internal` services from another module
- [ ] EF entities are **never** returned across a boundary (HTTP or in-proc) —
  always projected to a `*Response`/contract record (already §3d / §13; re-flag here
  as a _coupling_ defect, since a leaked entity ties the consumer to the schema)
- [ ] No `<ProjectReference>` from module A onto module B's base/EF assemblies — only
  onto B's `*.Abstractions` (verify with `MCP get_project_graph`)

**Prefer async events over synchronous calls:**

- [ ] A module's state changes are **published as events** through a mediator that is
  swappable for a real broker — local (`ILocalEventBus`, in-process) for same-pod
  flows, distributed (`IDistributedEventBus` → `*Eto` over Wolverine) for anything
  that must survive becoming a separate service. The choice is deliberate, not
  accidental (CLAUDE.md §Events, §Notifications routing)
- [ ] The in-memory mediator is an **abstraction over the bus**, so swapping to
  RabbitMQ/Kafka is a wiring change, not a code rewrite — no handler reaches around
  the bus into another module directly

**At-least-once delivery (under pod churn):**

- [ ] Distributed events (`*Eto`) are published via the **Wolverine durable
  outbox** (Transactional Outbox), so a message survives a pod crash between DB
  commit and publish — atomic with the `SaveChanges` that produced it
- [ ] Inbound message handlers are **idempotent** (dedup by message id / natural
  key) — at-least-once means a handler can see the same message twice
- [ ] No ordering assumption across partitions/replicas unless explicitly enforced
- [ ] Outbox/inbox tables live in the module's isolated DbContext, not a shared one

**If a synchronous query into another module is unavoidable:**

- [ ] It goes through a contract abstraction with **resilience baked in** (§15g —
  `IHttpClientFactory` + standard resilience handler: timeout, retry, circuit
  breaker), because the day B is extracted, that call becomes a K8s network hop

### 15d. Configuration & secrets (12-factor)

Config and secrets must be injectable per environment without rebuilding the image.

- [ ] Options bound from configuration (`SectionName` + `IOptions<T>`), overridable
  via env vars (`Granit__{Module}__{Key}`) — no compiled-in environment values
- [ ] **No hardcoded** hosts, URLs, ports, file paths, or connection strings; fix
  the registration site so SDK/cluster defaults align (see
  [[feedback_sdk_defaults_over_hardcoding]])
- [ ] **No secrets** in `appsettings*.json` or source — DB passwords, API keys,
  signing keys come from Vault / `ExternalSecret`-mounted env or files
- [ ] No `localhost`/`127.0.0.1`/`file://` defaults that only work on a dev box
- [ ] Feature toggles / endpoints overridable via `*Options` (e.g. `TagName`,
  health paths) rather than constants
- [ ] **Options shapes are env-var-overridable** — prefer dictionaries
  (`"Providers": { "Smtp": { … } }` → `…__Providers__Smtp__Host`) over arrays
  of objects (`"Providers": [ { "Name": "Smtp", … } ]` →
  `…__Providers__0__Name`): ordinal indices are fragile under Helm/ArgoCD
  overlays (an override targets a position, not an identity, and removing an
  element from the base config silently shifts every override). Flag a
  `List<T>`-of-records option where the elements have a natural key

**How to detect:** `grep -nE 'https?://(localhost|127\.0\.0\.1)|Password=|ApiKey|/home/|C:\\\\'`
across the module's `src` and `appsettings*.json`.

### 15e. Lifecycle: graceful startup & shutdown

Kubernetes sends SIGTERM, waits `terminationGracePeriodSeconds`, then SIGKILL. The
module must drain cleanly.

- [ ] In-flight work respects the host `CancellationToken` / `IHostApplicationLifetime`
  — no fire-and-forget `Task.Run` that is lost on shutdown
- [ ] `IHostedService`/`BackgroundService` implement `StopAsync` to drain (finish or
  re-enqueue) rather than drop work
- [ ] No `async void`, no `.Result`/`.Wait()` (deadlock + un-cancellable on drain) —
  already an anti-pattern (§2c), re-flag here through the shutdown lens
- [ ] Heavy one-time startup (migrations, warmup) is gated by the **startup probe**
  (§10d) and ideally moved to an init container / job, not run inline on every
  replica's hot path
- [ ] `IDisposable`/`IAsyncDisposable` released on shutdown (bus channels, file
  handles, DB connections)

### 15f. Health probes for the module's dependencies (k8s)

Builds on §10c/§10d, but from the _probe-correctness_ angle:

- [ ] The module **registers its own granular health contributions** via the
  `AddGranit*HealthCheck()` pattern, tagged `"readiness"` / `"startup"` so they are
  composed into the host's `/health/ready`, `/health/live`, `/health/startup`
  endpoints — a module that ships no health check for its own critical dependency is
  a gap (the host can't probe what the module never declared)
- [ ] The module contributes a **readiness** check for each external dependency it
  needs to serve traffic (its DbContext, blob backend, message broker, downstream
  service) — so k8s holds traffic until the dep is reachable
- [ ] Liveness check is **dependency-free** (a failing DB must not restart the pod —
  that is a readiness concern); only deadlock/unrecoverable state fails liveness
- [ ] Startup probe covers slow init (migrations) so liveness doesn't kill a
  still-booting pod
- [ ] **The app survives an un-migrated database** — when the schema is not yet
  in place (the K8s migration job/init container is still running), the module
  must fail its readiness/startup probe gracefully, NOT throw during
  `Program.cs`/module init. A startup-path schema query (`ValidateOnStart`
  hitting the DB, eager warmup, seeding) that throws turns "waiting for the
  migration job" into a CrashLoopBackOff — and on a fresh environment the
  migration job and the app race each other forever
- [ ] Probes are cheap + time-bounded (10s defensive timeout, `CachedHealthCheck`
  to avoid stampede across rapid kubelet polls) and leak **no PII/secrets/connection
  strings** (ISO 27001 / GDPR)

### 15g. Resilience to network failure

Everything is a remote call; the network is unreliable.

- [ ] `HttpClient` obtained from `IHttpClientFactory` (never `new HttpClient()`)
  **with the standard resilience handler** (`AddStandardResilienceHandler` / Polly):
  timeout, retry-with-jitter, circuit breaker
- [ ] DB / bus / blob calls have bounded timeouts — no unbounded `await` that hangs
  a request thread when a dependency is down
- [ ] Retries are only on **idempotent** operations, or paired with dedup
- [ ] Transient-fault handling does not mask a poisoned message into an infinite
  retry loop (dead-letter / max-attempts)

### 15h. Persistence in a distributed deployment

- [ ] **Isolated DbContext per module** (already §6a) — this is what makes
  DB-per-service / independent scaling possible; flag any shared DbContext
- [ ] Migrations are **not** auto-applied by every replica at boot (race →
  duplicate/locked migrations); they belong in a migration job / init container
  with single-runner semantics (framework packages ship **no** migrations — the
  app owns them: [[feedback_no_migrations_in_framework]])
- [ ] Optimistic concurrency (`IConcurrencyAware`, ADR-061
  [[project_optimistic_concurrency_adr_061]]) on entities with concurrent writers
  across replicas — last-writer-wins silent loss is a multi-replica data bug
- [ ] Connection-pool sizing is documented/aware that _effective_ connections =
  `MaxPoolSize × replica_count` (PostgreSQL/Npgsql default — see
  [[project_postgres_default_db]]); a module that opens many contexts per request
  amplifies this
- [ ] No advisory assumption of a single writer (e.g. in-app sequence generation)
  without a DB-backed/distributed guarantee

### 15h-bis. Inter-module data ownership & decoupling (CRITICAL — the EF Core trap)

This is usually the **strongest coupling point** in a .NET/EF Core framework and the
single biggest blocker to extracting a module as a service: shared data. A module
that owns its data can become a microservice; one whose tables are entangled with
another's cannot. Audit data ownership ruthlessly.

**No physical cross-module references — reference by ID, never by navigation:**

- [ ] An entity references another module's entity **only by its identifier**
  (`Guid OtherThingId`), **never** by an EF navigation property
  (`public OtherThing Thing { get; set; }`) or a configured FK that crosses the
  module boundary — a physical FK welds the two schemas together
- [ ] No `HasOne/WithMany`/`HasForeignKey` in a module's `*Configuration.cs` that
  targets another module's entity type
- [ ] No LINQ query `.Include()`-ing or `.Join()`-ing across two modules' tables —
  cross-module reads go through the other module's contract/event projection, not a
  SQL join (a join assumes one physical database forever)

**Schema / database segregation:**

- [ ] Each module's tables are **physically separable** — own DbContext (§6a/§15h)
  AND own schema (or own database) so the module can run against a separate
  connection string with no shared-table dependency (per-module / per-tenant schema
  isolation is what makes the split possible — see [[project_postgres_default_db]])
- [ ] No table is written by more than one module's DbContext (shared write surface =
  hidden coupling); a table read by another module is exposed via contract, not a
  second mapping
- [ ] Tenant isolation (`IMultiTenant`, `Guid? TenantId`) is enforced by the module's
  own filter, not by a shared/global query filter another module also depends on

**No cross-module distributed transactions — embrace eventual consistency:**

- [ ] A single `SaveChanges`/`SaveChangesAsync` **never spans two modules'
  DbContexts** — there is no two-phase commit across services; a flow that mutates
  module A and module B must be split into A-commits-then-publishes-event,
  B-reacts-and-commits
- [ ] Multi-module workflows use the **Transactional Outbox + eventual consistency**
  (publish `*Eto` atomically with A's commit via the Wolverine outbox; B consumes
  idempotently — §15c) rather than a synchronous "update both in one transaction"
- [ ] Where a saga/process-manager coordinates multi-step cross-module work,
  compensating actions exist for partial failure (no relational rollback will save
  you once the modules are separate services)

**How to detect:** `MCP get_project_graph` for cross-module `<ProjectReference>` onto
non-`Abstractions` assemblies; `MCP find_references` from one module's entities into
another; `grep` each `*Configuration.cs` for `HasForeignKey`/`HasOne` whose target
type lives in a different `Granit.{Module}`; scan for `.Include(`/`.Join(` bridging
two modules; check whether any two DbContexts map the same table name.

### 15i. Observability across service boundaries

- [ ] `ActivitySource` per module registered via `GranitActivitySourceRegistry`
  (§10b) so spans propagate W3C `traceparent` across services
- [ ] Outbound calls (HTTP, bus) propagate trace context; inbound handlers continue
  the trace rather than starting a fresh root
- [ ] Metrics tagged with `tenant_id` (coalesced `"global"`) and exportable via OTLP
  (`IMeterFactory`, never `new Meter`) — §10a
- [ ] Logs are structured (`[LoggerMessage]`), carry correlation/trace ids, and
  contain **no PII/secrets** (security baseline) — greppable across pods in a log
  aggregator
- [ ] No reliance on local log files as the record of truth (pods are ephemeral —
  stdout/OTLP only)

### 15j. Container & filesystem assumptions

- [ ] No persistent state on the local filesystem — durable data goes to the blob
  abstraction (S3/Azure), DB, or cache, never a pod-local path that vanishes on
  reschedule; temp/scratch files must be ephemeral and cleaned up
- [ ] No assumption of a fixed hostname, pod name, ordinal, or stable IP (unless the
  consuming service is explicitly a StatefulSet — note it, don't assume)
- [ ] No hardcoded thread-pool / memory sizing that fights cgroup limits — rely on
  .NET 10 container-aware defaults; flag manual `ThreadPool.SetMinThreads` /
  `GCHeapHardLimit` overrides that ignore the limit
- [ ] No process-affinity or "warm singleton" assumption that breaks when the pod
  is rescheduled to another node

### Severity guidance (microservices scope)

| Scenario | Severity |
| -------- | -------- |
| Cross-module FK / navigation property between two modules' entities | ARCHITECTURE |
| `SaveChanges` spans two modules' DbContexts (distributed transaction) | ARCHITECTURE |
| Cross-module `.Include()`/`.Join()` over another module's tables | ARCHITECTURE |
| Module exposes / returns its domain or EF entities across a boundary | ARCHITECTURE |
| `<ProjectReference>` onto another module's base/EF assembly (not Abstractions) | ARCHITECTURE |
| Two modules write the same physical table (shared write surface) | ARCHITECTURE |
| Singleton/recurring work runs per-replica (duplicate side effects) | ARCHITECTURE |
| Distributed event published without outbox (lost on crash) | ARCHITECTURE |
| Non-idempotent message handler under at-least-once delivery | ARCHITECTURE |
| Secret/connection string in source or `appsettings` | BREAKING |
| In-memory cache used as source of truth across replicas | ARCHITECTURE |
| Migrations auto-applied by every replica at boot | ARCHITECTURE |
| `new HttpClient()` / no resilience on external call | CONVENTION |
| Module ships no health check for its own critical dependency | CONVENTION |
| Liveness probe depends on an external dependency | CONVENTION |
| Startup throws on un-migrated DB (CrashLoopBackOff instead of failing readiness) | ARCHITECTURE |
| Array-of-objects options shape where elements have a natural key | CONVENTION |
| Hardcoded `localhost`/path/URL default | CONVENTION |
| Missing trace-context propagation on outbound call | CLEANUP |
| No graceful-shutdown drain on a hosted service | CONVENTION |

Ref: `CLAUDE.md §Anti-patterns`, `§Background Jobs`, `§Events`, `§Multi-tenancy`,
`§Security baseline`; global `CLAUDE.md §Security baseline` (Vault/ExternalSecret);
ADR-061; `docs-site/…/core/diagnostics.mdx`.

---

## Suppressions

Do NOT flag:

- Style-only issues (formatting, whitespace, brace placement) — handled by
  `dotnet format` and `/quality`
- Missing XML docs on `internal` members
- Test file organization or test naming style
- Generated files: `*.designer.cs`, EF migrations, `packages.lock.json`
- `packages.lock.json` changes
- Placeholder or skeleton modules under active development
- Code already covered by `Granit.ArchitectureTests` (note: verify coverage
  exists, do not re-check manually)
- SonarQube issues (handled by `/quality`)
