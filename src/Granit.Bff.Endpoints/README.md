# Granit.Bff.Endpoints

Minimal API endpoints for BFF (Backend For Frontend) authentication.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/bff/login` | OIDC authorization redirect with PKCE |
| GET | `/bff/callback` | Token exchange, session cookie creation |
| GET | `/bff/logout` | Session cleanup, RP-Initiated Logout |
| GET | `/bff/user` | Filtered user claims for the SPA |
| POST | `/bff/csrf-token` | CSRF token generation |

## Usage

```csharp
app.MapGranitBff();
```

## Security

- Login/callback endpoints include `X-Frame-Options: DENY` and
  `Content-Security-Policy: frame-ancestors 'none'` headers
- PKCE is mandatory on the login flow
- Tokens never reach the browser
- Session cookie: `HttpOnly + Secure + SameSite=Strict`
