# Granit.DataLookup.Endpoints

Minimal API endpoints for **Granit.DataLookup**.

## Routes

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/lookups` | Manifest of every registered source. |
| `GET` | `/lookups/{name}` | Paginated typeahead search (`?search=`, `?page=`, `?pageSize=`, `?scope.*=`). |
| `GET` | `/lookups/{name}/resolve` | Resolve a single value into a `LookupItem`. |

Labels in every response are already localized via the `Accept-Language` header — the
frontend never re-translates.

## Registration

```csharp
app.MapGranitDataLookups(opts =>
{
    opts.RoutePrefix = "lookups"; // default
    opts.TagName = "Data Lookup";            // default
});
```

## Authorization

The coarse policy `DataLookup.Lookups.Read` gates the entire group. Each source may
declare a stricter `RequiredPermission`, enforced by the handler before dispatch.
When a source declares scope keys (e.g. `tenantId`), missing values return `400` with
a `problem+json` body — the handler never falls back to an unscoped query.
