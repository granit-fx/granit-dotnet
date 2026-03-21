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
- [ ] `Granit.Core` is never listed in `[DependsOn]` (implicit base)
- [ ] Zero-dependency modules have no `[DependsOn]` attribute
- [ ] `.Endpoints` project exists if module exposes HTTP API
- [ ] `.EntityFrameworkCore` project exists if module has persistence
- [ ] Provider projects (`.S3`, `.AzureBlob`, etc.) implement abstractions only

Ref: `docs-site/…/concepts/module-system.mdx`

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

## 5. OpenAPI endpoint metadata (`--scope openapi`)

Every endpoint MUST declare all 5 elements:

- [ ] `.WithName("VerbNoun")` — PascalCase operation ID
- [ ] `.WithSummary("Imperative sentence.")` — ~100 chars, ends with period
- [ ] `.WithDescription("2-4 sentences...")` — what, context, errors
- [ ] `.Produces<T>()` — success response type (mapped from handler return type)
- [ ] `.ProducesProblem(StatusCodes.StatusXxx)` — one per error path in handler

### Return type mapping

| Handler return | Produces | ProducesProblem |
|----------------|----------|-----------------|
| `Ok<T>` | `.Produces<T>()` | — |
| `Created<T>` | `.Produces<T>(Status201Created)` | — |
| `Created` (no body) | `.Produces(Status201Created)` | — |
| `NoContent` | `.Produces(Status204NoContent)` | — |
| `Accepted` | `.Produces(Status202Accepted)` | — |
| `NotFound` in `Results` | — | `.ProducesProblem(Status404NotFound)` |
| `ProblemHttpResult` in `Results` | — | `.ProducesProblem(StatusXxx)` |
| `ValidationProblem` in `Results` | — | `.ProducesValidationProblem()` |
| `FileStreamHttpResult` | `.Produces(Status200OK, contentType: "...")` | — |

### Route groups

- [ ] `endpoints.MapGranitGroup(prefix)` — not `MapGroup()` (auto-validation)
- [ ] Route prefix is kebab-case and plural

### Schema examples

- [ ] `ISchemaExampleProvider` implemented for request DTOs with realistic values
- [ ] Internal-only endpoints marked with `[InternalApiAttribute]`

### API documentation

- [ ] Never Swashbuckle / NSwag — native `Microsoft.AspNetCore.OpenApi` only
- [ ] Scalar UI for documentation
- [ ] One OpenAPI document per API major version

Ref: `CLAUDE.md §OpenAPI metadata`, `docs-site/…/api/api-documentation.mdx`

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

- [ ] For distributed events: `[DependsOn(typeof(GranitEventBusWolverineModule))]`
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

17 cultures required: `en`, `fr`, `nl`, `de`, `es`, `it`, `pt`, `zh`, `ja`,
`pl`, `tr`, `ko`, `sv`, `cs`, `fr-CA`, `en-GB`, `pt-BR`

- [ ] All `src/*/Localization/**/*.json` files exist for all 17 cultures
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

- [ ] `using Granit.Core.MultiTenancy;` — not a hard reference to
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
