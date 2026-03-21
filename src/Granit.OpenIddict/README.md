# Granit.OpenIddict

Abstractions and core types for the Granit OpenIddict module family — a self-hosted OpenID Connect
authorization server built on [OpenIddict](https://openiddict.com/) and ASP.NET Core Identity.

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
|---------|---------|
| `Granit.OpenIddict.EntityFrameworkCore` | Entities, DbContext, tenant-isolated stores |
| `Granit.OpenIddict.Identity` | `IIdentityProvider` bridge over ASP.NET Core Identity |
| `Granit.OpenIddict.Endpoints` | Account self-service API |
| `Granit.OpenIddict.Admin.Endpoints` | Admin user/role/OIDC management API |
| `Granit.OpenIddict.Client` | External login providers (Google, Microsoft, GitHub) |
| `Granit.OpenIddict.Seeding` | Declarative OIDC application/scope seeding |
| `Granit.OpenIddict.BackgroundJobs` | Token cleanup + idle session enforcement |
| `Granit.OpenIddict.Passkeys` | WebAuthn/FIDO2 passwordless authentication |
| `Granit.Bundle.OpenIddict` | Meta-package pulling all of the above |
