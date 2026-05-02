# Granit.Documents.Endpoints

Minimal API endpoints for `Granit.Documents`: folder hierarchy CRUD with breadcrumb,
permission-gated routes, FluentValidation request validation, and OpenAPI metadata.

Phase 1 ships only the folder endpoints (F2.3). Document upload/download/version,
share ACL, tag, search, and quota endpoints arrive in subsequent stories of the
Granit.Documents Epic.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Documents`
- `Granit.Http.ApiDocumentation`
- `Granit.Validation`

## Wiring

In your host:

```csharp
builder.Services.AddGranitDocuments();
builder.AddGranitDocumentsEntityFrameworkCore(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Documents")));

// Endpoints — call MapGranitDocuments after routing has been registered.
app.MapGranitDocuments(o =>
{
    o.RoutePrefix = "documents";        // default
    o.TagName = "Documents";            // default
    o.RateLimitingPolicy = "DocumentsApi"; // optional — enforce a registered policy
});
```

## Permissions

| Permission | Use |
| --- | --- |
| `Documents.Folders.Read` | List folders, get one, breadcrumb. |
| `Documents.Folders.Manage` | Create / rename / move / trash folders. |

## Documentation

See [ADR-052](../../docs-site/src/content/docs/dotnet/architecture/adr/052-documents-module.md)
for the architecture decisions and the [full documentation](https://granit-fx.dev).
