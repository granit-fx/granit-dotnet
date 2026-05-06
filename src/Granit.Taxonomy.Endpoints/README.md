# Granit.Taxonomy.Endpoints

Minimal API endpoints for `Granit.Taxonomy`: Tag CRUD with per-scope
autocomplete, permission-gated routes, FluentValidation request validation,
and OpenAPI metadata.

Story T2.1 ships the Tag CRUD surface only. `TagAssignment` endpoints (T2.2),
the cross-entity search endpoint (T3.1), and Category endpoints (T4) arrive
in subsequent stories of the Granit.Taxonomy Epic.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Taxonomy.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Http.ApiDocumentation`
- `Granit.Taxonomy`
- `Granit.Validation`

## Wiring

In your host:

```csharp
builder.Services.AddGranitTaxonomy();
builder.AddGranitTaxonomyEntityFrameworkCore(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Taxonomy")));

// Endpoints — call MapGranitTaxonomy after routing has been registered.
app.MapGranitTaxonomy(o =>
{
    o.RoutePrefix = "taxonomy";          // default
    o.TagName = "Taxonomy";              // default
    o.RateLimitingPolicy = "TaxonomyApi"; // optional
});
```

## Endpoints

| Verb     | Route                                   | Permission              |
| -------- | --------------------------------------- | ----------------------- |
| `GET`    | `/api/v1/taxonomy/tags?scope=...&q=...` | `Taxonomy.Tags.Read`    |
| `GET`    | `/api/v1/taxonomy/tags/{id}`            | `Taxonomy.Tags.Read`    |
| `POST`   | `/api/v1/taxonomy/tags`                 | `Taxonomy.Tags.Manage`  |
| `PATCH`  | `/api/v1/taxonomy/tags/{id}`            | `Taxonomy.Tags.Manage`  |
| `DELETE` | `/api/v1/taxonomy/tags/{id}`            | `Taxonomy.Tags.Manage`  |

## Permissions

| Permission              | Use                                                |
| ----------------------- | -------------------------------------------------- |
| `Taxonomy.Tags.Read`    | List, get, autocomplete tags.                      |
| `Taxonomy.Tags.Manage`  | Create, rename, recolour, hide, and delete tags.   |

## Documentation

See [ADR-054](../../docs-site/src/content/docs/dotnet/architecture/adr/054-taxonomy-module.md)
for the architecture decisions and the [full documentation](https://granit-fx.dev).
