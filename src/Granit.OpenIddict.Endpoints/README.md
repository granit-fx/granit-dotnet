# Granit.OpenIddict.Endpoints

REST API for Granit.OpenIddict: account self-service and admin management.

## Account self-service (`/api/account`)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/account/register` | Anonymous | User registration |
| GET | `/api/account/profile` | Authenticated | Get current user profile |
| PUT | `/api/account/profile` | Authenticated | Update profile |
| POST | `/api/account/change-password` | Authenticated | Change password |
| POST | `/api/account/forgot-password` | Anonymous | Request password reset email |
| POST | `/api/account/reset-password` | Anonymous | Reset password with token |
| GET | `/api/account/confirm-email` | Anonymous | Confirm email address |
| POST | `/api/account/resend-confirmation-email` | Authenticated | Resend confirmation |
| GET | `/api/account/two-factor` | Authenticated | 2FA status |
| GET | `/api/account/two-factor/authenticator-key` | Authenticated | TOTP key + QR URI |
| POST | `/api/account/two-factor/enable` | Authenticated | Enable 2FA |
| POST | `/api/account/two-factor/disable` | Authenticated | Disable 2FA |
| POST | `/api/account/two-factor/reset-authenticator` | Authenticated | Reset TOTP key |
| POST | `/api/account/two-factor/recovery-codes` | Authenticated | Generate recovery codes |
| GET | `/api/account/external-logins` | Authenticated | List linked providers |
| DELETE | `/api/account/external-logins/{provider}` | Authenticated | Unlink provider |
| POST | `/api/account/delete` | Authenticated | Delete account (GDPR) |
| POST | `/api/account/session/heartbeat` | Authenticated | Reset idle timer |
| POST | `/api/account/session/back-to-impersonator` | Authenticated | End impersonation |

## Admin management (`/api/admin`)

### Users, Roles, Groups

Full CRUD on `/api/admin/users`, `/api/admin/roles`, `/api/admin/groups` with role/group
assignment and user impersonation. All protected by `OpenIddictPermissions.*`.

### OIDC Administration

CRUD on `/api/admin/oidc/applications`, `/api/admin/oidc/scopes`, `/api/admin/oidc/authorizations`
with secret rotation and per-user authorization revocation.

## Middleware order (mandatory)

```csharp
app.UseAuthentication();
app.UseOpenIddict();      // ← BEFORE UseAuthorization
app.UseAuthorization();
```
