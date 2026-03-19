---
title: "C# 14 Coding Standards & .NET Conventions"
description: C# 14 and .NET 10 coding standards for Granit — naming conventions, primary constructors, collection expressions, and architecture rules enforced by Roslyn analyzers.
sidebar:
  label: Coding Standards
  order: 2
---

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

### Background job naming (enforced by architecture tests)

| Interface | Attribute | Suffix | Example | Location |
| --------- | --------- | ------ | ------- | -------- |
| `IBackgroundJob` | `[RecurringJob]` | `*Job` | `OrphanBlobCleanupJob` | `Granit.{Module}/Jobs/` |

- `sealed record` implementing `IBackgroundJob`, decorated with `[RecurringJob("cron", "name")]`
- Job name format: `{module-kebab}-{action-kebab}` (e.g., `"blob-storage-orphan-cleanup"`)
- Handler in same `Jobs/` folder: `internal static partial class {Action}Handler`
- Never use `*Command` suffix for jobs — commands are CQRS

### Entity/API separation

EF Core entities must **never** be returned directly by an endpoint. Create a
`*Response` record that projects only the fields relevant to the consumer.

```csharp
// Correct -- dedicated Response record
public sealed record SavedViewResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string EntityType { get; init; }
}

// Wrong -- EF entity returned directly (leaks audit fields)
group.MapGet("/", () => TypedResults.Ok(efEntities));
```

**Exemptions**: shared cross-cutting types (`PagedResult<T>`, `ProblemDetails`)
do not need a module prefix.

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
- **Collection expressions (C# 12+)** -- `[]` for empty lists,
  `[.. enumerable]` for spread
- **Pattern matching** -- `is null`, `is not null` (never `== null`)
- **Target-typed `new()`** -- when the type is explicit on the left side

### Zero warnings

The project must compile with zero warnings. Warnings are latent bugs. Fix them
or suppress explicitly with `#pragma warning disable` plus a justification comment.

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

## XML documentation

- **Required** on all public types and members
- `<summary>` brief (1 line), `<remarks>` for detail
- `<inheritdoc/>` for interface implementations
- `<param>` and `<returns>` for public methods
- GDPR/ISO 27001 context in `<remarks>` when relevant

```csharp
/// <summary>
/// Exception representing a violated business rule.
/// Maps to <c>400 Bad Request</c>.
/// </summary>
/// <remarks>
/// Implements <see cref="IUserFriendlyException"/>: the message is safe to
/// expose to clients.
/// </remarks>
public class BusinessException : Exception
```

## Comments and TODOs

### Comments -- explain the "why", not the "what"

A comment has value only if it explains a non-obvious reason. Well-named code
describes *what it does* on its own.

### TODOs -- attribution and traceability required

Every `TODO` must include the **author** and a **GitHub issue number**:

```csharp
// TODO(JDO): Refactor this once we migrate to .NET 11 (Issue #452)
```

A `TODO` without an issue is noise. Create the issue first, then reference it.

### Forbidden comments

- **No commented-out code** -- use Git for history
- **No `// removed`** or `// unused` -- delete dead code
- **No separator banners** (`// ===== Section =====`) -- use `#region` or
  extract a class

## Async patterns

- **`Async` suffix** on all async methods
- **`CancellationToken`** as the last parameter with `= default`
- **`ConfigureAwait(false)`** in library code (NuGet packages)
- **`.WaitAsync(cancellationToken)`** for APIs without native CT support

```csharp
public async Task<string> DecryptAsync(
    string keyName,
    string ciphertext,
    CancellationToken cancellationToken = default)
{
    Secret<DecryptionResponse> result = await _vaultClient.V1.Secrets.Transit
        .DecryptAsync(keyName, requestOptions, mountPoint: _options.TransitMountPoint)
        .ConfigureAwait(false);
    // ...
}
```

## Time management

**Never** use `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, or
`DateTimeOffset.UtcNow`. Inject `TimeProvider` (native .NET 8+) or `IClock`
(Granit.Timing):

```csharp
// Correct
DateTimeOffset now = clock.Now;

// Wrong -- static call, not testable
DateTimeOffset now = DateTimeOffset.UtcNow;
```

## Logging

Use **`[LoggerMessage]` source-generated** logging -- never string
interpolation in log calls:

```csharp
[LoggerMessage(
    Level = LogLevel.Debug,
    Message = "Data encrypted with Transit key {KeyName}")]
private static partial void LogEncrypted(ILogger logger, string keyName);
```

Benefits: zero allocation when the log level is inactive, AOT-compatible,
compile-time verified placeholders.

## Regex

Use **`[GeneratedRegex]`** -- never `new Regex(..., RegexOptions.Compiled)`:

```csharp
[GeneratedRegex(@"^[a-z0-9-]+$", RegexOptions.None, 100)]
private static partial Regex SlugRegex();
```

The third parameter is a **timeout in milliseconds** -- mandatory for regex on
user input.

## Guard clauses

Use `ArgumentNullException.ThrowIfNull()` for programmer errors (internal null
checks). For user-facing validation, throw domain exceptions
(`ValidationException`, `BusinessException`, `EntityNotFoundException`):

```csharp
// Programmer error -- hidden 500 in production
ArgumentNullException.ThrowIfNull(service);

// User validation -- displayed in the UI (422)
if (string.IsNullOrWhiteSpace(email))
{
    throw new ValidationException(new Dictionary<string, string[]>
    {
        ["Email"] = ["The Email field is required."]
    });
}
```

:::tip[Pro tip]
Do not blindly convert `is null` + throw to `ThrowIfNull()`. If the code throws
a `BusinessException` or `ValidationException`, the message is intentionally
user-facing.
:::

## Error responses

All error responses must use `TypedResults.Problem()` (RFC 7807), never
`TypedResults.BadRequest<string>()`:

```csharp
// Correct -- structured ProblemDetails
return TypedResults.Problem(
    detail: "Invalid webhook payload.",
    statusCode: StatusCodes.Status400BadRequest);

// Wrong -- string body, no structured error
return TypedResults.BadRequest("Invalid webhook payload.");
```

The handler return type must reflect `ProblemHttpResult`:

```csharp
private static Task<Results<Ok, ProblemHttpResult>> HandleWebhookAsync(...)
```

## Endpoint conventions

- **Minimal API only** -- no MVC controllers
- Handlers must be **named static methods** (no inline lambdas)
- Every endpoint requires `.WithName()`, `.WithSummary()`, and `.WithTags()`
- Use `TypedResults` (not `Results`) for correct OpenAPI schema inference
- No anonymous return types -- create a typed record

## Banned APIs

The following APIs are banned at compile time via `BannedSymbols.txt`
(`Microsoft.CodeAnalysis.BannedApiAnalyzers`):

| Banned API | Alternative | Reason |
| ---------- | ----------- | ------ |
| `new HttpClient()` | `IHttpClientFactory` | Socket exhaustion |
| `new Regex(...)` | `[GeneratedRegex]` | AOT, performance |
| `Thread.Sleep()` | `Task.Delay()` | Blocks thread pool |
| `Task.Result` / `Task.Wait()` | `await` | Sync-over-async deadlock |
| `GC.Collect()` | -- | Forbidden in library code |
| `Console.Write/WriteLine` | `ILogger` + `[LoggerMessage]` | Structured observability |
| `Environment.GetEnvironmentVariable` | `IConfiguration` | Configuration injection |
