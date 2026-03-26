# Granit.Http.SecurityHeaders

HTTP security hardening for Granit APIs. Suppresses the Kestrel `Server` response
header, injects OWASP recommended security headers (X-Content-Type-Options,
X-Frame-Options, Referrer-Policy, Permissions-Policy, COOP, CORP), and configures
HSTS. OWASP ASVS V14.4, ISO 27001 A.8.9 compliant.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.SecurityHeaders
```

## Dependencies

- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
