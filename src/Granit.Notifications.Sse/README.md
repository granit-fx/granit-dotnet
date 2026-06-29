# Granit.Notifications.Sse

Server-Sent Events (SSE) real-time notification channel for Granit.Notifications. Uses native
.NET 10 `TypedResults.ServerSentEvents()` with `Channel<T>`-based connection management. Drop-in
replacement for `Granit.Notifications.SignalR` for unidirectional server-to-client push.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Notifications.Sse
```

## Dependencies

- `Granit.Notifications`

## Integration

Register the SSE channel in your module's `ConfigureServices` (registers
`ISseConnectionManager` and the SSE `INotificationChannel`):

```csharp
context.Services.AddGranitNotificationsSse();
```

Map the SSE endpoint on an endpoint route builder (e.g. your versioned API
group, as in the showcase, or directly on `app`):

```csharp
api.MapGranitSseNotifications();
```

Omitting either step disables SSE notifications: without
`AddGranitNotificationsSse` the channel and connection manager are never
registered, and without `MapGranitSseNotifications` the SSE endpoint is never
exposed.

## Documentation

See the [full documentation](https://granit-fx.dev).
