# Granit.Identity

Identity provider abstractions for Granit. Defines `IIdentityProvider` for querying
and managing users, roles, sessions, groups, device activity and password credentials
from external identity systems (Keycloak, Auth0, Entra ID, etc.). Includes models
(`IdentityUser`, `IdentityRole`, `IdentityGroup`, `IdentityUserCreate`) and a
`NullIdentityProvider` null-object registered by default.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity
```

## Dependencies

- `Granit`
- `Granit.Querying`

## Documentation

See the [full documentation](https://granit-fx.dev).
