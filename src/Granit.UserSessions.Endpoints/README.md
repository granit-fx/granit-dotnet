# Granit.UserSessions.Endpoints

The **canonical user-session HTTP API** for `granit`: one self-service surface for a
user to see and revoke their own sessions and devices — the same endpoints whether or
not the app uses a BFF, and whatever the identity provider (OpenIddict or Keycloak).

Part of the [granit](https://granit-fx.dev) framework.

## Endpoints

All require an authenticated caller and act on that caller's own data.

| Method | Route | Purpose |
| ------ | ----- | ------- |
| `GET` | `/sessions` | List the caller's sessions (location + risk, current flagged) |
| `DELETE` | `/sessions/{sessionId}` | Revoke one session |
| `DELETE` | `/sessions` | Revoke every session except the current one |
| `GET` | `/devices` | List the caller's devices |

Raw IP addresses are never returned — only the derived location.

## Usage

```csharp
app.MapGranitUserSessions();

// With a route prefix (e.g. /account/sessions):
app.MapGranitUserSessions(o => o.RoutePrefix = "account");
```

The endpoints delegate to `IUserSessionManager` (`Granit.UserSessions`), which lists and
revokes through the registered backend adapter. Install a backend integration package
(BFF, OpenIddict, Keycloak) to surface real sessions; without one the API responds with
empty lists.
