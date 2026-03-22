# Granit.Bff

Backend For Frontend security proxy abstractions for Granit applications.

## Overview

The BFF pattern keeps OIDC tokens server-side. The browser only receives a
`HttpOnly + Secure + SameSite=Strict` session cookie. All API requests are
proxied through the BFF, which attaches the Bearer token on the server.

## Key types

- `GranitBffOptions` — OIDC authority, client credentials, session configuration
- `IBffTokenStore` — distributed cache-backed token storage
- `IBffCsrfTokenGenerator` — HMAC-SHA256 anti-forgery token generation
- `BffTokenSet` — access, refresh, and ID tokens with expiry
- `BffMetrics` — OpenTelemetry counters for logins, logouts, proxy requests
- `BffActivitySource` — distributed tracing spans

## Related packages

- `Granit.Bff.Endpoints` — HTTP endpoints (login, callback, logout, user, CSRF)
- `Granit.Bff.Yarp` — YARP reverse proxy with token injection
