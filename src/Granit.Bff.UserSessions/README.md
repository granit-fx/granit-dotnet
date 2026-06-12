# Granit.Bff.UserSessions

Makes the BFF the backend for `granit`'s **canonical user-session API**. Install it
alongside `Granit.Bff` and `Granit.UserSessions.Endpoints` and a user's browser↔BFF
sessions surface — and revoke — through `/sessions`, with no BFF-specific client code.

Part of the [granit](https://granit-fx.dev) framework.

## What it does

Registers `BffUserSessionProvider` as the active `IUserSessionProvider` (replacing the
no-op default). It implements the contract over `IBffTokenStore`:

- **List** — every session of the user, aggregated across all configured frontends
  (id, created/last-activity, user-agent, IP → location is resolved by the manager).
- **Revoke one** — only a session that belongs to the caller (never another user's).
- **Revoke others** — every session except the caller's current one.

It carries no policy: authorization, audit and geo/risk enrichment stay in
`IUserSessionManager` (`Granit.UserSessions`).

## Usage

```csharp
// Modules: GranitBffModule + GranitUserSessionsModule + GranitBffUserSessionsModule
app.MapGranitUserSessions();   // /sessions now reads the BFF token store
```

Devices are not yet derived from BFF sessions — `/devices` stays empty under this
provider until a device-aggregation step is added.
