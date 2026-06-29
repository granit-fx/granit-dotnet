# Granit.Webhooks.Endpoints

Minimal API endpoints for administering Granit webhook subscriptions. CRUD operations, lifecycle management (suspend, resume, activate), secret rotation, delivery statistics, test pings, and queryable subscription/delivery lists. Protected by configurable role-based authorization.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Webhooks.Endpoints
```

## Dependencies

- `Granit.Webhooks`
- `Granit.Authorization`
- `Granit.QueryEngine.AspNetCore`
- `Granit.Validation`

## Integration

Map the webhook administration endpoints on your `IEndpointRouteBuilder` (or API
route group) after the Granit modules are loaded. Without these calls the
endpoints are never registered:

```csharp
api.MapGranitWebhooksConfig("webhooks");
api.MapGranitWebhooks().RequireAuthorization();
api.MapGranitWebhooksRedelivery("webhooks");
```

`MapGranitWebhooks()` is provided by this package; `MapGranitWebhooksConfig` and
`MapGranitWebhooksRedelivery` come transitively from `Granit.Webhooks`.

## Documentation

See the [full documentation](https://granit-fx.dev).
