# Granit.Bff.Endpoints

Minimal API endpoints for BFF (Backend For Frontend) authentication.

## Endpoints

| Method | Path | Description |
| --- | --- | --- |
| GET | `/bff/login` | OIDC authorization redirect with PKCE |
| GET | `/bff/callback` | Token exchange, session cookie creation |
| GET | `/bff/logout` | Session cleanup, RP-Initiated Logout |
| GET | `/bff/user` | Filtered user claims for the SPA |
| POST | `/bff/csrf-token` | CSRF token generation |

## Integration

Two prerequisites before mapping the endpoints:

1. **Register the module** on the host's root module:

   ```csharp
   [DependsOn(typeof(GranitBffEndpointsModule))]
   public sealed class MyAppModule : GranitModule { }
   ```

2. **Configure the `Bff` section** in `appsettings.json`. At minimum `Authority`
   (the OIDC server URL) is required — the startup validator
   (`GranitBffOptionsValidator`) throws if it is missing — plus a `Frontends`
   array:

   ```json
   {
     "Bff": {
       "Authority": "https://login.example.com",
       "Frontends": [
         {
           "Name": "spa",
           "ClientId": "spa-client",
           "ClientSecret": "<secret>",
           "Scopes": ["openid", "profile", "email", "offline_access"],
           "PathPrefix": "",
           "ClientUrl": "https://app.example.com"
         }
       ]
     }
   }
   ```

## Usage

Finally, map the BFF routes during endpoint registration:

```csharp
app.MapGranitBff();
```

## Security

- Login/callback endpoints include `X-Frame-Options: DENY` and
  `Content-Security-Policy: frame-ancestors 'none'` headers
- PKCE is mandatory on the login flow
- Tokens never reach the browser
- Session cookie: `HttpOnly + Secure + SameSite=Strict`
