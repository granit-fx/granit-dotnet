# Granit.UserSessions

The user-session **orchestrator** for `granit`: `IUserSessionManager`, the single,
topology-agnostic entry point for listing and revoking a user's sessions and devices.

Part of the [granit](https://granit-fx.dev) framework.

## Why

A client should call **one** session API regardless of deployment topology — with
or without a BFF, and whether the identity provider is OpenIddict or Keycloak. The
session data itself lives in different backends, so this module centralizes all
policy (authorization, audit, risk enrichment) in the manager and dispatches the
irreducible backend mechanism to a registered adapter
(`IUserSessionProvider` / `IUserDeviceProvider` from `Granit.UserSessions.Abstractions`).

## What it does

`IUserSessionManager`:

- **Lists** sessions through the registered backend provider and attaches each
  session's persisted risk verdict (`IUserSessionRiskStore`) — once, centrally.
- **Revokes** a single session or every session except the caller's current one,
  dispatching the command to the provider.
- **Lists devices** for the user.

Geolocation is resolved at session creation and stored on the session descriptor —
it is not recomputed here.

## Usage

```csharp
// The host registers a backend adapter (BFF / OpenIddict / Keycloak); the
// no-op defaults surface nothing until one is installed.
IReadOnlyList<UserSessionView> sessions =
    await manager.ListAsync(userId, currentSessionId, ct);

await manager.RevokeOthersAsync(userId, currentSessionId, ct);
```

Map the HTTP surface with `Granit.UserSessions.Endpoints`.
