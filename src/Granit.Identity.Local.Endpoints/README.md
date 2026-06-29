# Granit.Identity.Local.Endpoints

REST API for local identity account self-service: login, registration, profile,
password, 2FA, passkeys, external logins, GDPR account deletion, session
management, and admin impersonation.

Provider-agnostic — shared between `Granit.OpenIddict` and any future local
provider (e.g. Duende Identity Server).

## Quick start

```csharp
builder.AddGranit(granit => granit.AddOpenIddict());

// Map account self-service endpoints (on versioned API group)
api.MapGranitAccount();

// Map OpenIddict admin + account OIDC endpoints
api.MapGranitOpenIddict();
app.MapGranitOpenIddictServer();
```

> **Prerequisite:** the `granit.AddOpenIddict()` shorthand is defined only in the
> `Granit.Bundle.OpenIddict` package — add it, or the code fails to compile with
> `CS1061` (`GranitBuilder` has no `AddOpenIddict`). For manual wiring without the
> bundle, reference the individual `Granit.OpenIddict.*` modules and call
> `builder.AddGranitOpenIddict(options => options.UseNpgsql(connectionString))` from
> `Granit.OpenIddict.EntityFrameworkCore` — it transitively registers
> `GranitIdentityLocalEndpointsModule`, the OIDC server, and the AspNet Identity stack.

## Key endpoints

| Method | Path | Description |
| ------ | ---- | ----------- |
| `POST` | `/api/account/login` | Password login (sets Identity cookie) |
| `POST` | `/api/account/login/two-factor` | Complete 2FA challenge (TOTP or recovery) |
| `POST` | `/api/account/register` | Register new account |
| `POST` | `/api/account/passkeys/assertion/complete` | Passkey login (WebAuthn) |
| `GET` | `/api/account/profile` | Get user profile |
| `POST` | `/api/account/delete` | GDPR account deletion |

See the [Account Self-Service API](../../docs-site/src/content/docs/dotnet/security/openiddict/account-management-api.mdx) documentation for the complete reference.
