# Granit .NET — Audit Checklist

Verification matrix used by the `/audit` skill. Each category maps to a
`--scope` value. Apply in order.

Convention references point to:

- `CLAUDE.md` — project conventions (root of repo)
- `docs-site/…/dotnet/` — Astro documentation site
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
- [ ] Enum JSON serialization uses `JsonStringEnumConverter` (PascalCase)
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

Ref: `docs-site/…/data/concurrency.mdx`

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
|------------|------|
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
- [ ] Error code present in all 17 JSON files in
  `src/Granit.Validation/Localization/Validation/`

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
|-------|------|----------|
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
|------|----------|------|
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
`docs-site/src/content/docs/dotnet/`. The page lives under the appropriate
domain subdirectory:

| Domain | Directory | Example modules |
|--------|-----------|-----------------|
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

- [ ] `PACKAGE_COUNT` in `docs-site/src/data/constants.ts` matches
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
   `grep -r "OldModuleName" docs-site/src/content/`
3. Flag any occurrence as CONVENTION severity

Ref: `CLAUDE.md §Documentation site`

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
