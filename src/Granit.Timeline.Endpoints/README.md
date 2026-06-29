# Granit.Timeline.Endpoints

Minimal API endpoints for Granit.Timeline. Exposes paginated activity stream,
comment/note posting, soft-delete (GDPR), and follower management REST endpoints.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Timeline.Endpoints
```

## Dependencies

- `Granit.Authorization`
- `Granit.Timeline`

## Usage

Map the endpoints in your application startup (`Program.cs`):

```csharp
app.MapGranitTimeline();
// customize the prefix (default: "timeline"):
app.MapGranitTimeline(opts => opts.RoutePrefix = "admin/timeline");
```

Under the route prefix this registers: `GET /{entityType}/{entityId}`
(activity stream), `POST /{entityType}/{entityId}/entries` (comment / note),
`DELETE /{entityType}/{entityId}/entries/{id}` (GDPR soft-delete),
`POST` / `DELETE /{entityType}/{entityId}/follow`,
`GET /{entityType}/{entityId}/followers`, plus reaction routes. The route group
requires the `Timeline.Entries.Read` permission; individual endpoints may
require more (e.g. `Timeline.Entries.Create`).

## Documentation

See the [full documentation](https://granit-fx.dev).
