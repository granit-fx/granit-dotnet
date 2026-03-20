---
title: "C# 14 Coding Standards & .NET 10 Conventions"
description: C# 14 and .NET 10 coding standards for Granit — language features, naming conventions, DDD patterns, and architecture rules enforced by Roslyn analyzers.
sidebar:
  label: Coding Standards
  order: 2
---

## C# 14 features — use by default

- **Primary constructors**: for all DI service classes. Keep `private readonly T _x = x;`
  fields when the parameter is used in multiple methods.
- **Collection expressions**: `[x, y]` instead of `new[] { x, y }`, `[]` instead of
  `Array.Empty<T>()` or `new List<T>()`.
- **`field` keyword** (semi-auto properties): use `set => field = value;` in properties
  with custom setter logic instead of declaring a manual backing field.
- **Extension members** (`extension` blocks): prefer over static extension classes when
  adding multiple related members to the same type.
- **`nameof` on unbound generics**: `nameof(List<>)` returns `"List"`. NEVER use
  `nameof(T)` on a type parameter — use `typeof(T).Name` instead.
- **Pattern matching**: prefer `is`, `switch` expressions, list patterns, property patterns.
- **File-scoped namespaces**: always (`namespace X;`, no braces).

## C# 13 features — use by default

- **`System.Threading.Lock`**: ALWAYS use `private readonly Lock _lock = new();` for
  synchronization. NEVER lock on `object`, collections, or `this`.
- **`params ReadOnlySpan<T>`**: prefer over `params T[]` for non-attribute methods to
  reduce allocations. Attributes must keep `params T[]` (compile-time constraint).
- **`\e` escape**: use `\e` instead of `\u001b` or `\x1b` for ESCAPE characters.

## .NET 10 / EF Core 10 features — use by default

- **Named Query Filters** (EF Core 10): use `HasQueryFilter(name, expr)` for individually
  toggleable filters. Never use unnamed `HasQueryFilter(expr)`.
- **Native OpenAPI 3.1**: use `Microsoft.AspNetCore.OpenApi` + `AddOpenApi()` /
  `MapOpenApi()`. NEVER use Swashbuckle or NSwag. Scalar UI for documentation.
- **`IMeterFactory`**: use for metrics creation. NEVER use `new Meter(...)` directly.
- **`ActivitySource`**: native .NET diagnostics, one per module. Register via
  `GranitActivitySourceRegistry`.

## Naming conventions

| Element | Convention | Example |
| ------- | ---------- | ------- |
| Types | PascalCase, `sealed` by default | `sealed class AuditedEntityInterceptor` |
| Interfaces | `I` + PascalCase | `ICurrentUserService` |
| Methods | PascalCase, `Async` suffix for async | `EncryptAsync()` |
| Properties | PascalCase | `CreatedAt`, `TenantId?` |
| Private fields | `_camelCase` (underscore prefix) | `_currentUserService` |
| Constants | PascalCase (not UPPER_SNAKE) | `SectionName` |
| Parameters / locals | camelCase | `string errorCode` |
| Generics | `T` or `TPrefix` | `TModule`, `TEntity` |
| Options classes | `Options` suffix, `sealed` | `sealed class VaultOptions` |
| DI extensions | `Add*` / `Use*` | `AddGranitTiming()` |
| Module classes | `Module` suffix | `GranitTimingModule` |
| Enums | PascalCase values | `SequentialGuidType.AtEnd` |
| Endpoint DTOs | `[Module][Concept][Suffix]` | `WorkflowTransitionRequest` |

## DTO naming rules

OpenAPI flattens C# namespaces -- only the short type name appears in the schema.
Two modules exposing an `AttachmentInfo` will cause a conflict.

### Prefix with module context

Every public type used as an endpoint parameter or return value **must** carry a
prefix identifying its module:

| Wrong (too generic) | Correct (prefixed) | Module |
| ------------------- | ------------------ | ------ |
| `AttachmentInfo` | `TimelineAttachmentInfo` | Timeline |
| `ColumnMapping` | `ImportColumnMapping` | DataExchange |
| `TransitionRequest` | `WorkflowTransitionRequest` | Workflow |

### Required suffixes

| Suffix | Role | Example |
| ------ | ---- | ------- |
| `Request` | Input body (POST/PUT) | `CreateSavedViewRequest` |
| `Response` | Top-level return (GET, POST 201) | `UserNotificationResponse` |

:::caution[Warning]
The suffix `Dto` is **forbidden**. It conveys no information about the type's
role. Use `Request` or `Response` instead.
:::

### Event naming (enforced by architecture tests)

Two event categories with **mandatory suffixes**:

| Scope | Interface | Suffix | Example | Location |
| ----- | --------- | ------ | ------- | -------- |
| Domain (in-process) | `IDomainEvent` | `*Event` | `BlobValidatedEvent` | `Granit.{Module}/Events/` |
| Integration (distributed) | `IIntegrationEvent` | `*Eto` | `PersonalDataDeletedEto` | `Granit.{Module}/Events/` |

- `*Event` — raised via `AddDomainEvent()`, synchronous, same transaction
- `*Eto` (Event Transfer Object) — raised via `AddDistributedEvent()`, durable Wolverine outbox
- Use `sealed record` implementing the marker interface
- Past-tense verb + suffix (e.g., `BlobValidatedEvent`, not `BlobValidated`)
- Generic lifecycle: `EntityCreatedEvent<T>`, `EntityCreatedEto<T>` — automatic via
  `IEmitEntityLifecycleEvents`

### Background job naming (enforced by architecture tests)

| Interface | Attribute | Suffix | Example | Location |
| --------- | --------- | ------ | ------- | -------- |
| `IBackgroundJob` | `[RecurringJob]` | `*Job` | `OrphanBlobCleanupJob` | `Granit.{Module}/Jobs/` |

- `sealed record` implementing `IBackgroundJob`, decorated with `[RecurringJob("cron", "name")]`
- Job name format: `{module-kebab}-{action-kebab}` (e.g., `"blob-storage-orphan-cleanup"`)
- Handler in same `Jobs/` folder: `internal static partial class {Action}Handler`
- Never use `*Command` suffix for jobs — commands are CQRS

### Permission naming (STRICT)

All permission strings follow the `[Group].[Resource].[Action]` format — three dot-separated
segments, no exceptions.

| Segment | Convention | Example |
| ------- | ---------- | ------- |
| **Group** | PascalCase module name (`{Module}Permissions.GroupName`) | `BackgroundJobs`, `BlobStorage` |
| **Resource** | Nested static class (plural noun) | `Jobs`, `Blobs`, `Templates`, `Flags` |
| **Action** | Verb describing the access level | `Read`, `Manage`, `Execute`, `Create` |

**Standard actions:**

| Action | Meaning | Usage |
| ------ | ------- | ----- |
| `Read` | Read-only consultation | Always use `Read`, never `View` |
| `Manage` | Grouped write operations (create, update, delete) | Admin-level access |
| `Execute` | Single action (import, export, trigger) | Operation-level access |
| `Create` / `Update` / `Delete` | Granular CRUD (only when separate control is needed) | Fine-grained access |

**File structure per module:**

| File | Location |
| ---- | -------- |
| `{Module}Permissions.cs` | `Permissions/` |
| `{Module}PermissionDefinitionProvider.cs` | `Permissions/` (internal sealed) |
| `{Module}EndpointsLocalizationResource.cs` | `Internal/` |
| `{culture}.json` (17 files) | `Localization/{Module}Endpoints/` |

**Localization keys:**

- Group: `PermissionGroup:{Group}` (e.g., `PermissionGroup:BackgroundJobs`)
- Permission: `Permission:{Group}.{Resource}.{Action}` (e.g., `Permission:BackgroundJobs.Jobs.Read`)
### Metrics and diagnostics naming

Every module with observable operations SHOULD expose OpenTelemetry metrics via
a dedicated `*Metrics.cs` class and, when long-running or cross-process
operations exist, a `*ActivitySource.cs` class.

#### File placement

| File | Location | Example |
| ---- | -------- | ------- |
| `{Module}Metrics.cs` | `Diagnostics/` folder | `Diagnostics/BlobStorageMetrics.cs` |
| `{Module}ActivitySource.cs` | `Diagnostics/` folder | `Diagnostics/BlobStorageActivitySource.cs` |

Both classes live in the **base abstraction project** (`Granit.{Module}`), not in
a provider or EF Core sub-project, so all implementations can share the same
meter and activity source.

#### Metrics class conventions

| Element | Convention | Example |
| ------- | ---------- | ------- |
| Class | `sealed class {Module}Metrics` | `sealed class BlobStorageMetrics` |
| Meter name (const) | `"Granit.{Module}"` (PascalCase) | `"Granit.BlobStorage"` |
| Constructor | `IMeterFactory` injection (never `new Meter(...)`) | `BlobStorageMetrics(IMeterFactory meterFactory)` |
| Instrument fields | `private readonly` | `private readonly Counter<long> _uploadsInitiated;` |
| DI registration | `services.TryAddSingleton<{Module}Metrics>();` | In module's `Add*()` extension |

#### Metric naming (`granit.{module}.{entity}.{action}`)

| Segment | Format | Example |
| ------- | ------ | ------- |
| Prefix | `granit` (always) | `granit.` |
| Module | lowercase, no dots | `blobstorage`, `ratelimiting`, `dataexchange` |
| Entity | lowercase noun (plural for collections) | `blobs`, `requests`, `jobs`, `rows` |
| Action | lowercase past-tense verb or adjective | `completed`, `failed`, `active`, `duration` |

Full example: `granit.blobstorage.blobs.deleted`,
`granit.dataexchange.import.duration`

#### Instrument types

| Type | When to use | Unit |
| ---- | ----------- | ---- |
| `Counter<long>` | Monotonically increasing counts | `null` (dimensionless) |
| `UpDownCounter<long>` | Current state (active leases, connections) | `null` |
| `Histogram<double>` | Duration distributions (P50/P95/P99) | `"s"` (seconds) |

#### Tag conventions

| Tag | Format | Required | Example |
| --- | ------ | -------- | ------- |
| `tenant_id` | `snake_case` | **Always** (coalesce `null` to `"global"`) | `tenantId ?? "global"` |
| Operation-specific | `snake_case` | Per-metric | `status`, `channel`, `provider` |

Tags are passed via `TagList` (not `KeyValuePair` arrays) to avoid boxing
and allocation on hot paths.

#### Record methods

Public methods named `Record{Action}()` with minimal parameters. Use `TagList`
for zero-allocation tag passing:

```csharp
public void RecordDeleted(string? tenantId, string container) =>
    _blobsDeleted.Add(1, new TagList
    {
        { "tenant_id", tenantId ?? "global" },
        { "container", container },
    });
```

#### ActivitySource conventions

| Element | Convention | Example |
| ------- | ---------- | ------- |
| Class | `internal static class {Module}ActivitySource` | `DataExchangeActivitySource` |
| Name (const) | `"Granit.{Module}"` (PascalCase, matches Meter) | `"Granit.DataExchange"` |
| Operations | `{module-kebab}.{action}` (kebab-case) | `data-exchange.import.execute` |
| Registration | `GranitActivitySourceRegistry.Register(Name)` | In `Add*()` extension |

### Entity/API separation

EF Core entities must **never** be returned directly by an endpoint. Create a
`*Response` record that projects only the fields relevant to the consumer.

## DDD conventions

### Aggregate Root vs Entity

Use `AggregateRoot` (or audited variants) when the entity has a state machine, raises
domain events, or encapsulates invariants. Use plain `Entity` for append-only records,
configuration, caches, or lookup tables.

**Aggregate Root rules (enforced by `DomainConventionTests`):**

- **Private setters**: all properties `{ get; private set; }`. Use behavior methods for
  state transitions (e.g., `MarkAsValid()`, `Revoke()`)
- **Factory method**: `public static Xxx Create(...)` — the only way to construct
- **Private EF Core constructor**: keep `private Xxx() { }` for materialization
- **Domain events via base class**: use `AddDomainEvent()` / `AddDistributedEvent()` —
  NEVER manually implement `IDomainEventSource`
- **No public setters on aggregate roots** — architecture test enforces this

### Value Objects (`SingleValueObject<T>`)

- Inherit from `SingleValueObject<T>` for single-primitive wrappers
- Must be `sealed` with `init` properties
- Provide `Create()` factory with validation + implicit operators
- EF Core converters auto-applied by `ApplyGranitConventions`

## Isolated DbContext — MANDATORY for `*.EntityFrameworkCore` packages

Every isolated `DbContext` MUST:

1. `<ProjectReference>` to `Granit.Persistence`
2. Constructor-inject `ICurrentTenant?` and `IDataFilter?` (both optional, default `null`)
3. Call `modelBuilder.ApplyGranitConventions(currentTenant, dataFilter)` at end of
   `OnModelCreating`
4. Wire interceptors via `(sp, options)` overload of `AddDbContextFactory` (Scoped)
5. `[DependsOn(typeof(GranitPersistenceModule))]` on module class
6. **No manual `HasQueryFilter`** — `ApplyGranitConventions` handles all standard filters
7. `IMultiTenant` entities use `Guid? TenantId` (never `string`)

## Validation

- **Auto-validation**: use `endpoints.MapGranitGroup(prefix)` instead of `MapGroup()` —
  applies `FluentValidationAutoEndpointFilter` automatically
- **Validator discovery**: `GranitValidationModule` auto-discovers all `IValidator<T>` from
  loaded module assemblies (no manual registration needed)
- **Opt-out**: `.WithMetadata(new SkipAutoValidationAttribute())`
- **OpenAPI enrichment**: `FluentValidationSchemaTransformer` exposes validation constraints
  in the OpenAPI schema
- **Architecture tests**: `ValidationConventionTests` ensures all `*Request` types have
  validators and all route groups use `MapGranitGroup()`

## C# style rules

### `var` usage (IDE0008)

Use `var` when the type is apparent on the right side; explicit type otherwise.

```csharp
var stream = File.OpenRead("data.csv");           // type is apparent (FileStream)
var users = new Dictionary<int, User>();           // type is apparent (new)
ImportResult result = _service.ImportAsync(data);  // explicit -- type not obvious
```

### Expression body (IDE0022)

Use expression body (`=>`) for single-statement methods:

```csharp
public IReadOnlyList<Type> GetModuleTypes() =>
    [.. _modules.Select(m => m.ModuleType)];
```

### Braces are mandatory

Even for single-line blocks:

```csharp
// Correct
if (context is null)
{
    return;
}

// Wrong
if (context is null) return;
```

### Other style rules

- **Classes `sealed` by default** -- only leave unsealed when inheritance is
  explicitly intended
- **File-scoped namespaces** -- `namespace Granit.Vault.Services;`
- **Collection expressions** -- `[]` for empty, `[x, y]` for init, `[.. spread]`
- **Pattern matching** -- `is null`, `is not null` (never `== null`)
- **Target-typed `new()`** -- when the type is explicit on the left side
- **String interpolation** -- `$"..."` over `string.Concat()` or `string.Format()`

### Zero warnings

The project must compile with zero warnings. Warnings are latent bugs. Fix them
or suppress explicitly with `#pragma warning disable` plus a justification comment.

## Must-use patterns

| Pattern | Alternative (banned) |
| ------- | -------------------- |
| `[GeneratedRegex]` | `new Regex(..., Compiled)` |
| `[LoggerMessage]` | String interpolation in logs |
| `TimeProvider` / `IClock` | `DateTime.Now` / `UtcNow` |
| `ConfigureAwait(false)` in libraries | Missing ConfigureAwait |
| `ArgumentNullException.ThrowIfNull()` | Manual null checks |
| `ArgumentException.ThrowIfNullOrEmpty()` | Manual string checks |
| `TypedResults.Problem()` (RFC 7807) | `TypedResults.BadRequest<string>()` |
| `System.Threading.Lock` | `lock (object)` / `lock (this)` |
| `IMeterFactory` | `new Meter(...)` |
| `AddAuthorizationBuilder()` | `AddAuthorization(Action<>)` (ASP0025) |
| `HasQueryFilter(name, expr)` | Unnamed `HasQueryFilter(expr)` |

## File organization

### `using` order

System, then Microsoft, then project/third-party (enforced by `.editorconfig`):

```csharp
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Granit.Vault.Options;
using VaultSharp;
```

### File structure

1. Using statements
2. Namespace (file-scoped)
3. XML documentation on the type
4. Type declaration
5. Private readonly fields
6. Constructor (or primary constructor)
7. Public properties
8. Public methods
9. Private methods
10. Nested types (last)

**One type per file**, file name matches type name.

## Async patterns

- **`Async` suffix** on all async methods
- **`CancellationToken`** as the last parameter with `= default`
- **`ConfigureAwait(false)`** in library code (NuGet packages)
- **`async void`** is FORBIDDEN -- always return `Task`
- **`.Result` / `.Wait()`** is FORBIDDEN -- always `await`

## Logging

Use **`[LoggerMessage]` source-generated** logging -- never string
interpolation in log calls:

```csharp
[LoggerMessage(
    Level = LogLevel.Debug,
    Message = "Data encrypted with Transit key {KeyName}")]
private static partial void LogEncrypted(ILogger logger, string keyName);
```

## Regex

Use **`[GeneratedRegex]`** -- never `new Regex(..., RegexOptions.Compiled)`:

```csharp
[GeneratedRegex(@"^[a-z0-9-]+$", RegexOptions.None, 100)]
private static partial Regex SlugRegex();
```

The third parameter is a **timeout in milliseconds** -- mandatory for regex on
user input.

## Endpoint conventions

- **Minimal API only** -- no MVC controllers
- Handlers must be **named static methods** (no inline lambdas)
- Handlers suffixed `*Async` (e.g., `GetByIdAsync`, not `GetById`)
- Every endpoint requires `.WithName()` and `.WithSummary()`
- Use `TypedResults` (not `Results`) for correct OpenAPI schema inference
- No `.Produces<>()` / `.ProducesProblem()` -- TypedResults inference is sufficient
- Use `MapGranitGroup()` for auto-validation
- No anonymous return types -- create a typed record

## Banned APIs

The following APIs are banned at compile time via `BannedSymbols.txt`
(`Microsoft.CodeAnalysis.BannedApiAnalyzers`):

| Banned API | Alternative | Reason |
| ---------- | ----------- | ------ |
| `DateTime.Now` / `UtcNow` | `TimeProvider` / `IClock` | Not testable |
| `new HttpClient()` | `IHttpClientFactory` | Socket exhaustion |
| `new Regex(...)` | `[GeneratedRegex]` | AOT, performance |
| `Thread.Sleep()` | `Task.Delay()` | Blocks thread pool |
| `Task.Result` / `Task.Wait()` | `await` | Sync-over-async deadlock |
| `async void` | `async Task` | Unobserved exceptions |
| `new Meter(...)` | `IMeterFactory` | DI, testability |
| `lock (object)` | `System.Threading.Lock` | C# 13 typed lock |
| `GC.Collect()` | -- | Forbidden in library code |
| `Console.Write/WriteLine` | `ILogger` + `[LoggerMessage]` | Structured observability |
| `Environment.GetEnvironmentVariable` | `IConfiguration` | Configuration injection |
| Swashbuckle / NSwag | `Microsoft.AspNetCore.OpenApi` | Native .NET 10 |
