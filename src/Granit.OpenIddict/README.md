# Granit.OpenIddict

Abstractions and core types for the Granit OpenIddict module — a self-hosted
OpenID Connect authorization server built on OpenIddict and ASP.NET Core Identity.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.OpenIddict
```

## What's in this package

- **`GranitOpenIddictModule`** — module class with `[DependsOn]` declarations
- **`GranitOpenIddictOptions`** — static configuration (issuer, reference tokens, entity caching)
- **`OpenIddictSettingNames`** — per-tenant settings (token lifetimes, max login attempts, idle timeout)
- **`OpenIddictFeatureNames`** — feature flags (2FA, device flow, external logins, PKCE, passkeys)
- **`OpenIddictPermissions`** — RBAC permission constants (`[Group].[Resource].[Action]`)
- **Service interfaces** — `ITotpService`, `IEmailConfirmationService`, `IAccountDeletionService`, `IExternalLoginService`
- **Integration events** — `UserRegisteredEto`, `AccountDeletedEto`, `UserImpersonatedEto`
- **Impersonation extensions** — `ClaimsPrincipal.IsImpersonated()`, `FindImpersonatorUserId()`
- **Diagnostics** — `OpenIddictMetrics` (IMeterFactory) + `OpenIddictActivitySource`

## Related packages

| Package | Purpose |
| --- | --- |
| `Granit.OpenIddict.Server` | OIDC server configuration |
| `Granit.OpenIddict.EntityFrameworkCore` | Entities, DbContext, tenant-isolated stores |
| `Granit.Identity.Local.AspNetIdentity` | `IIdentityProvider` bridge over ASP.NET Core Identity |
| `Granit.Authentication.OpenIddict` | Authentication handler configuration |
| `Granit.OpenIddict.Endpoints` | Account self-service and admin API |
| `Granit.OpenIddict.BackgroundJobs` | Token cleanup + idle session enforcement |
| `Granit.Bundle.OpenIddict` | Meta-package pulling all of the above |

## Dependencies

- `Granit`
- `Granit.Events`
- `Granit.Guids`
- `Granit.Identity`
- `Granit.QueryEngine`
- `Granit.Users`
- `Granit.Timing`

## Documentation

See the [full documentation](https://granit-fx.dev).
