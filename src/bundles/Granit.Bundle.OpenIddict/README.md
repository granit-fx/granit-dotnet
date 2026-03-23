# Granit.Bundle.OpenIddict

Meta-package grouping all Granit OpenIddict modules for a complete self-hosted
OpenID Connect authorization server.

Part of the [granit](https://granit-fx.dev) framework.

## Included packages

| Package | Role |
| --- | --- |
| `Granit.OpenIddict` | Abstractions, interfaces, options |
| `Granit.OpenIddict.Server` | OIDC server configuration |
| `Granit.OpenIddict.EntityFrameworkCore` | Entities, DbContext, tenant-isolated stores |
| `Granit.Identity.Local.AspNetIdentity` | `IIdentityProvider` bridge |
| `Granit.Authentication.OpenIddict` | Authentication handler configuration |
| `Granit.OpenIddict.Endpoints` | Account and admin REST API |
| `Granit.OpenIddict.BackgroundJobs` | Token cleanup, idle session enforcement |

## Installation

```bash
dotnet add package Granit.Bundle.OpenIddict
```

## Documentation

See the [full documentation](https://granit-fx.dev).
