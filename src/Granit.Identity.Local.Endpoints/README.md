# Granit.Identity.Local.Endpoints

REST API for local identity account self-service: login, registration, profile,
password, 2FA, passkeys, external logins, GDPR account deletion, session
management, and admin impersonation.

Provider-agnostic — shared between `Granit.OpenIddict` and any future local
provider (e.g. Duende Identity Server).

## Quick start

```csharp
builder.AddGranit(granit => granit.AddOpenIddict());

// Map account self-service endpoints (/api/account)
app.MapGranitAccount();

// Map OpenIddict OIDC endpoints (/connect/*, /api/admin/oidc)
app.MapGranitOpenIddict();
app.MapGranitOpenIddictServer();
```

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
