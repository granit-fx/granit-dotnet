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

This exposes an anonymous, cacheable `GET cookies/config` endpoint (the final
path depends on your API group and versioning, e.g. `/api/v1/cookies/config`)
returning the registered internal cookies and third-party services for
CMP/cookie-banner initialization. Optionally customize the route prefix and
OpenAPI tag via the `Action<CookieConsentEndpointsOptions>` overload. Without
this call the package adds no endpoints.

## Dependencies

- `Granit.Http.Cookies`
- `Granit`

## Documentation

See the [full documentation](https://granit-fx.dev).
