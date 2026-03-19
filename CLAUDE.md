# CLAUDE.md - Granit

## Project

- **Type**: Modular .NET framework — 128 packages, 134 test projects
- **Repo**: `granit-dotnet` (company-level, not product-specific)
- **License**: Apache-2.0 (open-source)
- **Compliance**: GDPR + ISO 27001
- **Publication**: nuget.org (planned), GitHub Packages (internal)

## Stack & versions

.NET 10 (LTS) | C# 14 | EF Core 10 | VaultSharp 1.17+ | Serilog 9+ | OpenTelemetry 1.15+

## Architecture

```text
src/
  Granit.Core/                             # Module system, shared domain types
  Granit.{Module}/                         # Abstractions + DI registration (e.g. Granit.BlobStorage)
  Granit.{Module}.Endpoints/               # Minimal API endpoints
  Granit.{Module}.EntityFrameworkCore/      # Isolated DbContext, EF configurations, migrations
  Granit.{Module}.{Provider}/              # Provider implementations (e.g. .S3, .AzureBlob, .Keycloak)
  Granit.{Module}.Wolverine/               # Wolverine message handlers
  Granit.{Module}.Notifications/           # Notification channel integration
  bundles/
    Granit.Bundle.{Name}/                  # Meta-packages grouping related modules

tests/
  Granit.{Module}.Tests/                   # Unit tests (xUnit + Shouldly + NSubstitute + Bogus)
  Granit.{Module}.Tests.Integration/       # Integration tests (Testcontainers)
  Granit.ArchitectureTests/                # Cross-cutting architecture rules (NetArchTest)

templates/
  granit-api/                              # dotnet new template: minimal API project
  granit-api-full/                         # dotnet new template: full API project
  granit-module/                           # dotnet new template: new Granit module

docs-site/                                 # Astro + Starlight documentation site
```

### Package naming convention

One project = one NuGet package. Namespace = project name. Zero circular refs.

Discover packages: `ls src/` — do NOT rely on a hardcoded list.

### Module anatomy

Each module follows a consistent layered split:

| Layer | Project suffix | Contains |
| ----- | -------------- | -------- |
| Abstractions | `Granit.{Module}` | Interfaces, options, DI extension, `*Module` class |
| Endpoints | `.Endpoints` | Minimal API route groups, request/response DTOs |
| Persistence | `.EntityFrameworkCore` | Isolated `DbContext`, entity configs, migrations |
| Provider | `.{Provider}` | External service implementation (S3, Keycloak, SMTP...) |
| Messaging | `.Wolverine` | Wolverine handlers, saga state machines |

## Commands

```bash
# Full solution
dotnet build
dotnet test
dotnet format --verify-no-changes

# Single package
dotnet build src/Granit.BlobStorage
dotnet test tests/Granit.BlobStorage.Tests

# Architecture tests
dotnet test tests/Granit.ArchitectureTests

# Pack for local feed
dotnet pack -c Release -o ./nupkgs

# Docs site
cd docs-site && npx astro build   # must produce 0 errors
```

## Code quality

Modern C# 14 with idiomatic .NET 10. Write code that a senior .NET developer would recognize
as current and clean. Actively use the latest language and runtime features listed below.

### C# 14 features — use by default

- **Primary constructors**: for all DI service classes. Keep `private readonly T _x = x;`
  fields when the parameter is used in multiple methods.
- **Collection expressions**: `[x, y]` instead of `new[] { x, y }`, `[]` instead of
  `Array.Empty<T>()` or `new List<T>()`. Exception: `new[]` inside anonymous types
  (JSON payloads) where the compiler cannot infer the target type.
- **`field` keyword** (semi-auto properties): use `set => field = value;` in properties
  with custom setter logic instead of declaring a manual backing field.
- **Extension members** (`extension` blocks): prefer over static extension classes when
  adding multiple related members to the same type (especially FluentValidation
  `IRuleBuilder<T,P>` extensions).
- **`nameof` on unbound generics**: `nameof(List<>)` returns `"List"` — use for concrete
  generic types. NEVER use `nameof(T)` on a type parameter (returns the param name
  `"T"`, not the runtime type name — use `typeof(T).Name` instead).
- **Pattern matching**: prefer `is`, `switch` expressions, list patterns, property patterns.
- **File-scoped namespaces**: always (one `namespace X;` per file, no braces).

### C# 13 features — use by default

- **`System.Threading.Lock`**: ALWAYS use `private readonly Lock _lock = new();` for
  synchronization. NEVER lock on `object`, collections, or `this`.
- **`params ReadOnlySpan<T>`**: prefer over `params T[]` for non-attribute methods to
  reduce allocations. Attributes must keep `params T[]` (compile-time constraint).
- **`\e` escape**: use `\e` instead of `\u001b` or `\x1b` for ESCAPE characters.

### .NET 10 / EF Core 10 features — use by default

- **Named Query Filters** (EF Core 10): use `HasQueryFilter(name, expr)` for individually
  toggleable filters. See `GranitFilterNames` constants. Never use unnamed
  `HasQueryFilter(expr)`.
- **Native OpenAPI 3.1**: use `Microsoft.AspNetCore.OpenApi` + `AddOpenApi()` /
  `MapOpenApi()`. NEVER use Swashbuckle or NSwag. Scalar UI for documentation.
- **`IMeterFactory`**: use for metrics creation. NEVER use `new Meter(...)` directly.
- **`ActivitySource`**: native .NET diagnostics, one per module
  (`{Module}ActivitySource.cs`). Register via `GranitActivitySourceRegistry`.
- **`string.Split(char, count)`**: prefer `"a:b".Split(':', 2)` over `Split(new[]{':'}, 2)`
  (except in `netstandard2.0` source generators).

## Code conventions

Full standards: [`docs/guide/conventions/`](docs/guide/conventions/index.md)

### Must-use patterns

- **`var`**: when type is apparent; explicit type otherwise (IDE0008)
- **Expression body** (`=>`): for single-statement methods (IDE0022)
- **String interpolation**: prefer `$"..."` over `string.Concat(...)` or `string.Format(...)`
- **`[GeneratedRegex]`**: ALWAYS — never `new Regex(..., Compiled)`. Timeout on user input.
- **`[LoggerMessage]`**: ALWAYS — never string interpolation in log calls
- **`TimeProvider` / `IClock`**: NEVER `DateTime.Now`/`UtcNow`
- **`ConfigureAwait(false)`**: in library code. `CancellationToken` as last param.
- **`ArgumentNullException.ThrowIfNull()`**: over manual null checks
- **`ArgumentException.ThrowIfNullOrEmpty()`** / `ThrowIfNullOrWhiteSpace()`: for strings
- **`AddAuthorizationBuilder()`**: not `AddAuthorization(Action<>)` (ASP0025)

### Events — naming convention (STRICT)

Two event categories with **mandatory suffixes** — enforced by architecture tests:

| Scope | Interface | Suffix | Example | Dispatched |
| ----- | --------- | ------ | ------- | ---------- |
| Domain (local, in-process) | `IDomainEvent` | `*Event` | `BlobValidatedEvent` | After commit (`SavedChanges`) |
| Integration (distributed, outbox) | `IIntegrationEvent` | `*Eto` | `PersonalDataDeletedEto` | Before commit (`SavingChanges`) for Wolverine outbox |

- **`*Event`**: raised via `AddDomainEvent()` — synchronous, same transaction, handlers
  run after commit. Past-tense verb + `Event` suffix.
- **`*Eto`** (Event Transfer Object): raised via `AddDistributedEvent()` — durable,
  persisted in Wolverine outbox atomically. Past-tense verb + `Eto` suffix.
- **Generic lifecycle**: `EntityCreatedEvent<T>`, `EntityCreatedEto<T>` — automatic via
  `IEmitEntityLifecycleEvents` marker interface.
- **NEVER** use bare past-tense names without suffix (`BlobValidated` is wrong,
  `BlobValidatedEvent` is correct).

### Background Jobs — naming convention (STRICT)

Single category with **mandatory suffix** — enforced by architecture tests:

| Interface | Attribute | Suffix | Example | Location |
| --------- | --------- | ------ | ------- | -------- |
| `IBackgroundJob` | `[RecurringJob]` | `*Job` | `OrphanBlobCleanupJob` | `Granit.{Module}/Jobs/` |

- **`*Job`**: `sealed record` implementing `IBackgroundJob`, decorated with
  `[RecurringJob("cron", "name")]`. Handler in same `Jobs/` folder.
- **Job name format**: `{module-kebab}-{action-kebab}` (e.g., `"blob-storage-orphan-cleanup"`).
  Module prefix ensures global uniqueness.
- **Handler naming**: `{Action}Handler` (e.g., `OrphanBlobCleanupHandler`) — `internal static partial class`.
- **NEVER** use `*Command` suffix for jobs — commands are CQRS, jobs are scheduled work units.
- **NEVER** create a separate `.Wolverine` package for jobs — jobs live in the base module's
  `Jobs/` folder. Wolverine scheduling is handled by `Granit.BackgroundJobs.Wolverine`.

### DTOs & API responses

- **Prefixed names**: `WorkflowTransitionRequest`, not `TransitionRequest` — OpenAPI flattens namespaces
- **Suffixes**: `*Request` for input, `*Response` for output. NEVER `*Dto`.
- **Errors**: Always `TypedResults.Problem(detail, statusCode)` (RFC 7807). Return type: `ProblemHttpResult`.
- **No entity exposure**: EF entities must NOT be returned — create `*Response` records.

### Validation

- **Auto-validation**: use `endpoints.MapGranitGroup(prefix)` instead of `MapGroup()` — applies
  `FluentValidationAutoEndpointFilter` automatically to all endpoints in the group
- **Validator discovery**: `GranitValidationModule` auto-discovers all `IValidator<T>` from
  loaded module assemblies (no manual registration needed)
- **Opt-out**: `group.MapPost("/x", Handler).WithMetadata(new SkipAutoValidationAttribute())`
- **OpenAPI enrichment**: `FluentValidationSchemaTransformer` exposes validation constraints
  (maxLength, pattern, required, etc.) in the OpenAPI schema for frontend code generators
- **Architecture tests**: `ValidationConventionTests` ensures all `*Request` types have
  validators and all route groups use `MapGranitGroup()`

### Isolated DbContext — MANDATORY for `*.EntityFrameworkCore` packages

Every isolated `DbContext` MUST:

1. `<ProjectReference>` to `Granit.Persistence`
2. Constructor-inject `ICurrentTenant?` and `IDataFilter?` (both optional, default `null`)
3. Call `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` at end of `OnModelCreating`
4. Wire interceptors via `(sp, options)` overload of `AddDbContextFactory` (Scoped), resolving `AuditedEntityInterceptor` / `SoftDeleteInterceptor`
5. `[DependsOn(typeof(GranitPersistenceModule))]` on module class
6. **No manual `HasQueryFilter`** — `ApplyGranitConventions` handles all standard filters
7. `IMultiTenant` entities use `Guid? TenantId` (never `string`)

Reference: [`docs/framework/data/persistence.md`](docs/framework/data/persistence.md)

### DDD — Aggregate Root vs Entity

Use `AggregateRoot` (or audited variants) when the entity has a state machine, raises
domain events, or encapsulates invariants. Use plain `Entity` for append-only records,
configuration, caches, or lookup tables.

**Aggregate Root rules (enforced by `DomainConventionTests`):**

- **Private setters**: all properties `{ get; private set; }`. Use behavior methods for
  state transitions (e.g., `MarkAsValid()`, `Revoke()`)
- **Factory method**: `public static Xxx Create(...)` — the only way to construct
- **Private EF Core constructor**: keep `private Xxx() { }` for materialization
- **Explicit interface for `IMultiTenant`**: when `TenantId` has `private set`, add
  explicit `Guid? IMultiTenant.TenantId { get; set; }` for interceptor injection
- **Domain events via base class**: use `AddDomainEvent()` / `AddDistributedEvent()` —
  NEVER manually implement `IDomainEventSource`
- **No public setters on aggregate roots** — architecture test enforces this

**Value Objects (`SingleValueObject<T>`):**

- Inherit from `SingleValueObject<T>` for single-primitive wrappers
- Must be `sealed` with `init` properties
- Provide `Create()` factory with validation + implicit operators for backward compat
- EF Core converters auto-applied by `ApplyGranitConventions` (no migration needed)
- JSON serialization handled by `SingleValueObjectJsonConverterFactory`

Reference: [ADR-017](docs-site/src/content/docs/dotnet/architecture/adr/017-ddd-aggregate-value-object-strategy.md)

### Multi-tenancy — soft dependency

`ICurrentTenant` lives in `Granit.Core.MultiTenancy` — available everywhere without referencing `Granit.MultiTenancy`.

- Use `using Granit.Core.MultiTenancy;` — do NOT add `[DependsOn(GranitMultiTenancyModule)]`
- Always check `IsAvailable` before using `Id` — `NullTenantContext` is the default
- Hard dependency on `Granit.MultiTenancy` only when enforcing strict tenant isolation (GDPR)

### `[DependsOn]` convention

- **Direct = declare it.** Every `<ProjectReference>` with a `*Module` needs a `[DependsOn]`
- **Transitive = omit it.** Already pulled in by another declared dependency → skip
- **`Granit.Core`** = implicit base, never needs `DependsOn`
- **Alphabetical order** for `DependsOn` entries
- **Zero-dependency modules** have no `[DependsOn]` attribute — this is correct

### Tests

Each package has `*.Tests` project (xUnit + Shouldly + NSubstitute + Bogus). Part of DoD.

## Anti-patterns — NEVER do this

### Code

- `DateTime.Now`/`UtcNow` → inject `TimeProvider`
- `new Regex(..., Compiled)` → `[GeneratedRegex]`
- String interpolation in logs → `[LoggerMessage]`
- `new HttpClient()` → `IHttpClientFactory`
- `async void` → always return `Task`
- `.Result` / `.Wait()` → `await`
- `Results.Ok()` → `TypedResults.Ok()` for OpenAPI
- Return EF entities from endpoints → map to `*Response` DTOs
- Bare `catch (Exception)` → catch specific types
- `*Dto` suffix → use `*Request` / `*Response`
- `TypedResults.BadRequest<string>()` → `TypedResults.Problem()` (RFC 7807)
- `lock (object)` / `lock (collection)` → `lock (Lock)` with `System.Threading.Lock`
- `new Meter(...)` → inject `IMeterFactory` and call `meterFactory.Create(...)`
- `Array.Empty<T>()` / `new List<T>()` / `new T[] {}` → `[]` (collection expression)
- `string.Concat(a, b, c)` → `$"{a}{b}{c}"` (string interpolation)
- `params T[]` in non-attribute methods → `params ReadOnlySpan<T>`
- `nameof(T)` on type parameter → `typeof(T).Name` (nameof returns `"T"`, not the type name)
- Traditional constructors with only field assignments → primary constructors
- Unnamed `HasQueryFilter(expr)` → named `HasQueryFilter(name, expr)` (EF Core 10)
- Swashbuckle / NSwag → `Microsoft.AspNetCore.OpenApi` + Scalar UI
- Domain event without `Event` suffix → `BlobValidatedEvent` (not `BlobValidated`)
- Integration event without `Eto` suffix → `PersonalDataDeletedEto` (not `PersonalDataDeletedEvent`)
- Public setters on aggregate roots → `private set` + behavior methods
- Manual `IDomainEventSource` implementation → inherit from `AggregateRoot` (or variants)
- `new XxxEntity { ... }` on aggregate roots → `XxxEntity.Create(...)` factory method

### Architecture

- Merge `I*Reader`/`I*Writer` into combined `I*Store` → CQRS, keep them separate
- Share DbContext across modules → each module owns its isolated DbContext
- Manual `HasQueryFilter` in entity configs → `ApplyGranitConventions` handles all filters
- Cross-module direct method calls → use integration events (Wolverine)
- Repository pattern over EF Core → use DbContext directly
- Circular project references → restructure dependencies

### Refactoring

Code that looks "weird" almost always exists for a reason: production fix, regulatory
edge case, GDPR/ISO 27001 constraint, third-party workaround.

**Before any refactoring:**

1. Read the entire file, not just the targeted function
2. Check `git log -p -- <file>` to understand evolution
3. If unclear, search for the linked GitHub issue before modifying
4. When in doubt, **ask**

**NEVER:**

- Delete "dead" code without verifying dynamic references (reflection, DI, runtime config)
- Simplify complex conditions without testing the edge cases they cover
- Replace custom implementations with stdlib without understanding why stdlib wasn't used
- Remove/change interface implementations on `ValueObject`/`Entity`/`AggregateRoot` for SonarQube — mark as won't fix
- Reduce constructor params via wrapper types that aren't real domain concepts — mark `brain-overload` as won't fix

## Compliance

1. **GDPR**: Minimization, right to erasure, pseudonymization
2. **ISO 27001**: Audit trail, encryption at rest and in transit
3. **Secrets**: No plaintext secrets, mandatory rotation

## Localization

See [`docs/guide/conventions/langues.md`](docs/guide/conventions/langues.md) for full rules.

17 cultures — 14 base (en, fr, nl, de, es, it, pt, zh, ja, pl, tr, ko, sv, cs) + 3 regional (fr-CA, en-GB, pt-BR). Every `src/*/Localization/**/*.json` must exist for all 17. Regional files only contain differing keys.

## Documentation site

The docs live in `docs-site/` (Astro + Starlight). Key paths:

| Path | Content |
| ---- | ------- |
| `docs-site/src/content/docs/reference/modules/` | .NET module reference (one `.mdx` per module) |
| `docs-site/src/content/docs/reference/frontend/` | Frontend SDK reference |
| `docs-site/src/content/docs/architecture/patterns/` | Design patterns |
| `docs-site/src/content/docs/architecture/adr/` | ADRs |
| `docs-site/src/data/constants.ts` | Counters (update when adding packages/patterns/ADRs) |

**When creating a new module**: create `.mdx` in `reference/modules/`, update `PACKAGE_COUNT` in `constants.ts`, add "See also" links from related pages.

## MCP — Roslyn Navigator

Use `roslyn-navigator` MCP tools for semantic navigation — **prefer over Grep/Read for all C# code**.
Full guide in global `~/.claude/CLAUDE.md`.

## Definition of Done

See [`docs/guide/conventions/dod.md`](docs/guide/conventions/dod.md)

**NEVER push or create an MR** without: tests passing, docs updated, `dotnet format --verify-no-changes`, markdownlint clean. These are **blocking**. Refuse until satisfied or user explicitly overrides.
