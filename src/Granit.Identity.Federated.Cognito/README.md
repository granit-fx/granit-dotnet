# Granit.Identity.Federated.Cognito

AWS Cognito User Pools implementation of `IIdentityProvider` for Granit. Supports
user CRUD, group management (Cognito groups serve as both roles and groups), session
revocation via global sign-out, password reset, temporary password assignment, and
credential verification via `ADMIN_USER_PASSWORD_AUTH` flow.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Identity.Federated.Cognito
```

## Documentation

See the [full documentation](https://granit-fx.dev).
