# Granit.Authentication

Cross-scheme authentication primitives for Granit: shared claims transformations and
role-claim normalization helpers consumed by `Granit.Authentication.*` and
`Granit.OpenIddict.*` resource-server packages so that the framework's
`ClaimTypes.Role` contract (read by `Granit.Authorization.PermissionChecker`) holds
across authentication schemes.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
