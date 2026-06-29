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

## Integration

This is the abstractions package; the OpenIddict module only activates once it is
pulled into the host's module graph and wired in the composition root.

1. Declare the module on your host module — the sibling server/EFC/endpoints
   modules resolve transitively, so you only list this one:

   ```csharp
   [DependsOn(typeof(GranitOpenIddictModule))]
   public sealed class AppHostModule : GranitModule { }
   ```

2. Call the mandatory entry point in your composition root. This registers
   ASP.NET Core Identity, OpenIddict core/server/validation, the isolated
   `OpenIddictDbContext`, the session/device provider, and the query-engine sources:

   ```csharp
   builder.AddGranitOpenIddict(options => options.UseNpgsql(connectionString));
   ```

   (Defined in `Granit.OpenIddict.EntityFrameworkCore.Extensions`.)

3. Expose the OIDC server protocol endpoints:

   ```csharp
   app.MapGranitOpenIddictServer();
   ```

4. Expose the account/admin API on your route group:

   ```csharp
   api.MapGranitOpenIddict();
   ```

## Documentation

See the [full documentation](https://granit-fx.dev).
