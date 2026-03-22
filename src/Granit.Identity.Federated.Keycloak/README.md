# Granit.Identity.Federated.Keycloak

Keycloak Admin REST API implementation of `IIdentityProvider` for Granit. Supports
user CRUD, role management (assign/remove realm roles), session termination, group
membership, password reset, device activity (via OAuth 2.0 token exchange + Account API),
and custom user attributes.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.Keycloak
```

## Dependencies

- `Granit.Identity.Federated`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
