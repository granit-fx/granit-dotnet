---
title: "HTTP Conventions \u2014 REST API Standards"
description: HTTP conventions for Granit APIs — status codes, RFC 7807 Problem Details, DTO naming, pagination, and OpenAPI-compatible response formats.
sidebar:
  label: HTTP Conventions
  order: 31
---

This page documents the HTTP conventions used across all Granit endpoints. These
conventions ensure consistent behavior for API consumers, generated clients, and
OpenAPI documentation.

## Status codes

### Success (2xx)

| Code | Name | When to use | Response body |
|------|------|-------------|---------------|
| 200 | OK | Request processed, result in response | Yes |
| 201 | Created | Resource created | Yes + `Location` header |
| 202 | Accepted | Asynchronous processing started | Yes (tracking ID or job reference) |
| 204 | No Content | Operation succeeded, nothing to return | No |
| 207 | Multi-Status | Batch operation with mixed results | Yes (`BatchResult<T>`) |

**200 vs 202**: Use 200 when the response is immediate and complete. Use 202 when
processing is deferred (export generation, GDPR erasure, background jobs). A 202
response must always include a tracking mechanism (body with `requestId`/`jobId`,
`Location` header for polling, or webhook notification).

**204 use cases**: `DELETE` operations, `PUT /settings`, acknowledgment endpoints
like `POST /notifications/mark-read`.

### Client errors (4xx)

| Code | Name | When to use | Module |
|------|------|-------------|--------|
| 400 | Bad Request | Syntactically invalid request (malformed JSON, wrong type) | — |
| 401 | Unauthorized | Token absent or expired | [Authentication](/dotnet/security/authentication/) |
| 403 | Forbidden | Authenticated but not authorized | [Authorization](/dotnet/security/authorization/) |
| 404 | Not Found | Resource not found (routes with `{id}` parameter) | — |
| 409 | Conflict | Concurrency conflict (stale version, duplicate); also concurrent idempotent request | [Idempotency](/dotnet/api/idempotency/) |
| 422 | Unprocessable Entity | Business validation failed (FluentValidation rules) | [ExceptionHandling](/dotnet/api/exception-handling/) |
| 429 | Too Many Requests | Rate limiting triggered | [RateLimiting](/dotnet/api/rate-limiting/) |

### Server errors (5xx)

| Code | Name | When to use | Module |
|------|------|-------------|--------|
| 500 | Internal Server Error | Unexpected server-side error | [ExceptionHandling](/dotnet/api/exception-handling/) |
| 502 | Bad Gateway | Upstream service unavailable (Keycloak, Vault, S3) | — |
| 503 | Service Unavailable | Bulkhead full — tenant has too many concurrent operations in flight | [Bulkhead](/dotnet/api/bulkhead/) |

## RFC 7807 Problem Details

All error responses (4xx and 5xx) use the `application/problem+json` content type
defined by [RFC 7807](https://tools.ietf.org/html/rfc7807).

### Required pattern

Always use `TypedResults.Problem()`. Never use `TypedResults.BadRequest<string>()`.

```csharp
// Correct
return TypedResults.Problem(
    detail: "The template name exceeds 200 characters.",
    statusCode: StatusCodes.Status422UnprocessableEntity);

// Wrong - produces application/json, not application/problem+json
return TypedResults.BadRequest("The template name exceeds 200 characters.");
```

The Roslyn analyzer **GRAPI002** flags `TypedResults.BadRequest` calls that include
a body argument and offers an automatic code fix.

### Response format

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation failed",
  "status": 422,
  "detail": "The NISS format is invalid.",
  "instance": "/api/v1/patients"
}
```

### Return type declarations

Use union types so OpenAPI generates correct response schemas:

```csharp
private static async Task<Results<Ok<TemplateDetailResponse>, NotFound, ProblemHttpResult>>
    HandleGetDetailAsync(/* parameters */)
```

The `ProblemDetailsResponseOperationTransformer` automatically declares error
responses in the OpenAPI documentation.

### Validation errors

`FluentValidationEndpointFilter<T>` returns `422 Unprocessable Entity` with
`HttpValidationProblemDetails` containing structured error codes:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "One or more validation errors occurred.",
  "status": 422,
  "errors": {
    "Name": ["Granit:Validation:NotEmptyValidator"]
  }
}
```

## DTO naming conventions

### Suffixes

| Suffix | Usage | Example |
|--------|-------|---------|
| `*Request` | Input bodies (POST/PUT/PATCH) | `TemplateCreateRequest` |
| `*Response` | Top-level return types | `TemplateDetailResponse` |

Never use the `*Dto` suffix. EF Core entities must never be returned directly from
endpoints. Always create a `*Response` record.

### Module prefix

Module-specific DTOs must be prefixed with their module context. OpenAPI flattens
C# namespaces, so generic names cause schema conflicts.

```csharp
// Correct - prefixed with module context
public sealed record WorkflowTransitionRequest(string TargetState);
public sealed record TemplateCreateRequest(string Name, string Content);

// Wrong - ambiguous in flattened OpenAPI schema
public sealed record TransitionRequest(string TargetState);
public sealed record CreateRequest(string Name, string Content);
```

Shared cross-cutting types are exempt from the prefix rule: `PagedResult<T>`,
`ProblemDetails`, `BatchResult<T>`.

## Pagination

Granit supports two pagination modes via `QueryDefinition<T>`.

### Offset pagination (default)

```text
GET /api/v1/patients?page=1&pageSize=20&sort=-createdAt
```

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | `int` | `1` | Page number (1-based) |
| `pageSize` | `int` | `20` | Page size (capped at `MaxPageSize`, default 100) |
| `sort` | `string` | Per `QueryDefinition` | Comma-separated fields, `-` prefix for descending |
| `skipTotalCount` | `bool` | `false` | Skip the `COUNT(*)` query for large tables |

Response:

```json
{
  "items": [],
  "totalCount": 142,
  "hasMore": true
}
```

When `skipTotalCount=true`, `totalCount` is `null` and `hasMore` is determined by
fetching `pageSize + 1` rows.

### Cursor pagination (keyset)

Opt-in mode for real-time feeds, infinite scroll, and large datasets. Enabled via
`SupportsCursorPagination()` in the `QueryDefinition<T>`.

```text
GET /api/v1/events?pageSize=50
GET /api/v1/events?cursor=eyJpZCI6MTIzfQ&pageSize=50
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `cursor` | `string` | Opaque cursor (Base64). Absent for first page |
| `pageSize` | `int` | Page size |

Response:

```json
{
  "items": [],
  "totalCount": null,
  "nextCursor": "eyJpZCI6MTczfQ",
  "hasMore": true
}
```

The cursor is opaque. Clients must never decode or construct it.

### PagedResult type

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int? TotalCount,
    bool HasMore,
    string? NextCursor = null);
```

### Choosing between modes

| Criterion | Offset | Cursor |
|-----------|--------|--------|
| Page number navigation | Yes | No |
| Total count | Yes (opt-out available) | No |
| Frequently changing data | Results may skip/duplicate | Stable |
| Performance on large tables | `OFFSET N` degrades with N | Constant (`WHERE id > X`) |
| Infinite scroll / real-time | Not recommended | Recommended |

An endpoint can support both modes when the `QueryDefinition` declares
`SupportsCursorPagination()`. The mode is determined by the presence of the
`cursor` parameter.

## Sorting

Comma-separated field names. Prefix `-` for descending order:

```text
GET /api/v1/patients?sort=-createdAt,lastName
```

Only fields declared `Sortable()` in the `QueryDefinition` are accepted. An
unsortable field returns **400 Bad Request**.

## Filtering

### Field filters

Syntax: `filter[field.operator]=value`

```text
GET /api/v1/patients?filter[name.contains]=Alice&filter[age.gte]=18
```

Supported operators: `eq`, `contains`, `startsWith`, `endsWith`, `gt`, `gte`,
`lt`, `lte`, `in`, `between`.

### Presets

```text
GET /api/v1/patients?presets[status]=active,pending
```

### Quick filters

```text
GET /api/v1/patients?quickFilters=MyPatients,Unread
```

### Global search

```text
GET /api/v1/patients?search=Alice
```

Searches fields declared via `GlobalSearch()` in the `QueryDefinition`.

## Content types

| Content type | Usage |
|-------------|-------|
| `application/json` | Default for all request/response bodies |
| `application/problem+json` | All error responses (RFC 7807) |
| `multipart/form-data` | File uploads |
| `application/octet-stream` | File downloads |

### JSON conventions

- **Properties**: `camelCase` (default `System.Text.Json` behavior)
- **Enums**: PascalCase strings via `JsonStringEnumConverter` (e.g., `"InProgress"`)
- **Dates**: ISO 8601 with timezone (`2025-03-15T10:30:00Z`)
- **Identifiers**: UUID v7 with dashes (`"0193a5b2-7c3d-7def-8a12-bc3456789abc"`)

## Batch operations (207 Multi-Status)

For operations processing multiple resources where individual items can fail
independently:

```json
{
  "results": [
    { "value": { "id": "abc", "status": "Created" }, "isSuccess": true, "error": null },
    { "value": null, "isSuccess": false, "error": { "status": 422, "detail": "Invalid format" } }
  ],
  "successCount": 1,
  "failureCount": 1
}
```

Use 207 for: data imports, bulk updates, multi-recipient notifications. Do not
use 207 for single-resource operations or transactional (all-or-nothing) operations.

## Endpoint conventions

### URL structure

- **Resource segments**: `kebab-case` (`/background-jobs`, `/reference-data`)
- **Route parameters**: `camelCase` (`{patientId}`, `{tenantId}`)
- **Prefix**: `/api` is an application-level decision, not enforced by the framework

```csharp
// Application with UI - /api prefix
var api = app.MapGroup("api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

// Pure API service - no prefix
var api = app.MapGroup("v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);
```

### Route groups

Endpoints are organized by `MapGranitGroup` with OpenAPI tags. `MapGranitGroup()`
replaces `MapGroup()` and automatically applies FluentValidation to all endpoints
in the group:

```csharp
RouteGroupBuilder group = endpoints.MapGranitGroup(prefix)
    .RequireAuthorization()
    .WithTags("MobilePush");
```

### OpenAPI endpoint metadata

Every endpoint must declare **5 metadata elements** for a complete OpenAPI schema.
These metadata elements power Scalar UI documentation, generated TypeScript clients,
and frontend autocompletion.

#### Required chain (in order)

```csharp
group.MapGet("/{id:guid}", GetByIdAsync)
    .WithName("GetBlobDescriptor")
    .WithSummary("Returns a blob descriptor by its unique identifier.")
    .WithDescription(
        "Fetches the full metadata of a blob including its status, content type, "
        + "size, and validation results. The containerName query parameter is required. "
        + "Returns 404 if the blob does not exist in the specified container.")
    .Produces<BlobDescriptorResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);
```

| Element | Purpose | Convention |
|---------|---------|------------|
| `.WithName()` | OpenAPI `operationId` | PascalCase `VerbNoun` (e.g., `GetBlobDescriptor`, `CreateExportJob`) |
| `.WithSummary()` | One-line description in Scalar UI list | Imperative sentence, ends with period, ~100 chars |
| `.WithDescription()` | Expanded description in Scalar UI detail | 2-4 sentences: what it does, context/behavior, error codes |
| `.Produces<T>()` | Success response schema | One per success path (see mapping table below) |
| `.ProducesProblem()` | Error response schema | One per distinct error status code |

#### Return type to Produces mapping

The handler's return type determines which `.Produces()` and `.ProducesProblem()`
calls are needed:

| Handler return type | Produces | ProducesProblem |
|---------------------|----------|-----------------|
| `Ok<T>` | `.Produces<T>()` | — |
| `Created<T>` | `.Produces<T>(StatusCodes.Status201Created)` | — |
| `Created` (no body) | `.Produces(StatusCodes.Status201Created)` | — |
| `NoContent` | `.Produces(StatusCodes.Status204NoContent)` | — |
| `Accepted` | `.Produces(StatusCodes.Status202Accepted)` | — |
| `NotFound` in `Results<>` | — | `.ProducesProblem(StatusCodes.Status404NotFound)` |
| `ProblemHttpResult` in `Results<>` | — | `.ProducesProblem(StatusCodes.StatusXxx)` |
| `ValidationProblem` in `Results<>` | — | `.ProducesValidationProblem()` |
| `FileStreamHttpResult` | `.Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")` | — |

For `ProblemHttpResult`, read the handler body to determine the exact status code
used in `TypedResults.Problem(statusCode: ...)`.

#### Examples by endpoint type

**Read (single resource)**:

```csharp
group.MapGet("/{id:guid}", GetByIdAsync)
    .WithName("GetApiKeyById")
    .WithSummary("Returns an API key by its unique identifier.")
    .WithDescription("Fetches the full details of an API key. Returns 404 if not found.")
    .Produces<ApiKeyResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);
```

**Create**:

```csharp
group.MapPost("/", CreateAsync)
    .WithName("CreateApiKey")
    .WithSummary("Creates a new API key.")
    .WithDescription(
        "Generates a new API key with the specified scopes. "
        + "The plain-text key is returned once and cannot be retrieved later.")
    .Produces<ApiKeyCreateResponse>(StatusCodes.Status201Created);
```

**Delete**:

```csharp
group.MapDelete("/{id:guid}", DeleteAsync)
    .WithName("DeleteWebhookSubscription")
    .WithSummary("Deletes a webhook subscription.")
    .WithDescription("Permanently removes the subscription. This action is irreversible.")
    .Produces(StatusCodes.Status204NoContent);
```

**Async dispatch (background job)**:

```csharp
group.MapPost("/{jobId:guid}/execute", ExecuteAsync)
    .WithName("ExecuteImportJob")
    .WithSummary("Dispatches the import job for background execution.")
    .WithDescription(
        "Enqueues the job for background processing. Returns 202 Accepted. "
        + "Returns 400 if the job is in an invalid state, or 404 if not found.")
    .Produces(StatusCodes.Status202Accepted)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status400BadRequest);
```

**File download**:

```csharp
group.MapGet("/jobs/{jobId:guid}/download", DownloadAsync)
    .WithName("DownloadExportFile")
    .WithSummary("Downloads the generated export file.")
    .WithDescription("Streams the file as a binary download. Returns 404 if not found.")
    .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
    .ProducesProblem(StatusCodes.Status404NotFound);
```

### Automatic validation

All endpoints within a `MapGranitGroup()` route group are automatically validated.
For each request argument, the filter resolves `IValidator<T>` from DI and validates
if one exists. No per-endpoint wiring is needed:

```csharp
// Validated automatically — no .ValidateBody<T>() needed
group.MapPost("/", HandleCreate);
group.MapPut("/{id}", HandleUpdate);
```

Validators are auto-discovered from all loaded module assemblies by
`GranitValidationModule`. To opt out of validation on a specific endpoint, use
`SkipAutoValidationAttribute`:

```csharp
group.MapPost("/special", HandleSpecial)
    .WithMetadata(new SkipAutoValidationAttribute());
```

## Idempotency

The `Granit.Http.Idempotency` package provides Stripe-style idempotency via the
`Idempotency-Key` HTTP header, backed by Redis.

### Header

| Header | Format | Description |
|--------|--------|-------------|
| `Idempotency-Key` | Client-generated string (typically UUID) | Unique key per operation |

### Behavior

| Scenario | Response |
|----------|----------|
| First request | Executes handler, caches response for 24h |
| Duplicate with same body | Replays cached response + `X-Idempotency-Replayed: true` |
| Duplicate with different body | `422` with detail "payload does not match" |
| Concurrent duplicate | `409` with `Retry-After` header |
| Missing required key | `422` with detail about missing header |
| Multipart request | `422` (non-deterministic boundaries prevent hashing) |

### Configuration

```json
{
  "Idempotency": {
    "HeaderName": "Idempotency-Key",
    "CompletedTtl": "1.00:00:00",
    "InProgressTtl": "00:00:30",
    "ExecutionTimeout": "00:00:25"
  }
}
```

Cached status codes: 2xx + 400, 404, 409, 410, 422. Authentication errors
(401/403) are never cached.

## API versioning

URL-segment versioning with query string fallback, powered by `Asp.Versioning`.

### Version readers (priority order)

1. **URL segment**: `/api/v{version:apiVersion}/resource` (primary)
2. **Query string**: `?api-version=1.0` (fallback, visible in access logs)

### Configuration

```json
{
  "ApiVersioning": {
    "DefaultMajorVersion": 1,
    "ReportApiVersions": true
  }
}
```

When `ReportApiVersions` is `true`, every response includes
`api-supported-versions` and `api-deprecated-versions` headers.

### Deprecation headers

When an endpoint or API version is deprecated:

| Header | Format | Description |
|--------|--------|-------------|
| `Deprecation` | `true` | Endpoint is deprecated |
| `Sunset` | HTTP-date (RFC 7231) | Date after which endpoint will be removed |
| `Link` | `<url>; rel="deprecation"` | Link to migration documentation |

## CORS

Configured via the `Cors` section in `appsettings.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["https://app.example.com", "https://admin.example.com"],
    "AllowCredentials": false
  }
}
```

The default policy allows any header and any method for the configured origins.
Wildcard (`*`) origins are rejected in non-development environments (ISO 27001
compliance).

## Cacheability

| Endpoint type | Cache-Control | Rationale |
|---------------|---------------|-----------|
| User data (`GET /me`, `/settings`) | `private, no-cache` | Personal data, must revalidate |
| Reference data (`GET /reference-data`) | `public, max-age=3600` | Rarely changes, shared |
| Paginated lists (`GET /patients`) | `private, no-store` | Sensitive data (GDPR) |
| Static resources (images, documents) | `public, max-age=86400, immutable` | Content-addressable |
| Query metadata (`GET /meta`) | `public, max-age=3600` | Stable configuration |

GDPR constraint: responses containing personal data (PII) must never use `public`
in `Cache-Control`. Use `private` or `no-store` to prevent intermediate caches
(CDN, reverse proxy) from storing sensitive data.
