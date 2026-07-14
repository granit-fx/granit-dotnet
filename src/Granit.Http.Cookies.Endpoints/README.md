# Granit.Http.Cookies.Endpoints

Minimal API endpoints for `Granit.Http.Cookies`: exposes registered cookies and third-party
service definitions to the front-end for CMP configuration. Public, anonymous endpoint.
GDPR/ISO 27001 compliant.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Cookies.Endpoints
```

## Usage

After adding the package, map the endpoint on your endpoint route builder (or
Granit API group):

```csharp
app.MapGranitCookieConsent();
```

This exposes two anonymous endpoints (the final path depends on your API group
and versioning, e.g. `/api/v1/cookies/...`):

- `GET cookies/config` — cacheable; returns the registered internal cookies and
  third-party services for CMP/cookie-banner initialization.
- `POST cookies/consent` — appends the user's consent decision to the server-side
  ledger (`IConsentLedger`, GDPR Art. 7(1) accountability). The client IP is
  irreversibly anonymized (`IpAddressAnonymizer`) and the user-agent truncated at
  capture; the endpoint is idempotency-aware and guarded by the `cookie-consent`
  rate-limiting policy (configure it under `RateLimiting:Policies:cookie-consent`).
  Returns `204 No Content`; category names are validated against the snake_case
  vocabulary of `GET cookies/config` (localized messages in 18 cultures).

Optionally customize the route prefix and OpenAPI tag via the
`Action<CookieConsentEndpointsOptions>` overload. Without this call the package
adds no endpoints.

## Dependencies

- `Granit.Http.Cookies`
- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
