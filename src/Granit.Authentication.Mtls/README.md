# Granit.Authentication.Mtls

Server-side mutual-TLS certificate-bound access token validation (RFC 8705) for ASP.NET Core JWT Bearer authentication. IdP-agnostic — works with OpenIddict, Keycloak, Entra ID, Auth0, or any OIDC provider. Verifies that the presented client certificate matches the token's `cnf.x5t#S256` confirmation claim, so a leaked token cannot be replayed without the client's private key.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Authentication.Mtls
```

## Usage

```csharp
builder.Services.AddGranitMtlsValidation();

app.UseAuthentication();
app.UseGranitMtlsValidation(); // must run after UseAuthentication()
app.UseAuthorization();
```

## Configuration

```jsonc
{
  "Authentication": {
    "Mtls": {
      // true — every authenticated request must carry a certificate-bound token (FAPI 2.0).
      // false (default) — unbound tokens pass through; bound tokens are still verified.
      "RequireCertificateBinding": false
    }
  }
}
```

## Dependencies

- `Granit`
- `Granit.Diagnostics`

## Documentation

See the [full documentation](https://granit-fx.dev).
