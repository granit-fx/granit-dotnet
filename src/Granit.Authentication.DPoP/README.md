# Granit.Authentication.DPoP

Server-side DPoP (RFC 9449) proof validation for ASP.NET Core JWT Bearer authentication. IdP-agnostic — works with Keycloak, Entra ID, Auth0, OpenIddict, or any OIDC provider. Validates DPoP proof JWTs, cnf.jkt token binding (RFC 7638), and optional nonce replay protection.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.DPoP
```

## Dependencies

- `Granit.Core`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
