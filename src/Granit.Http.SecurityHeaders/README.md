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
- `Granit.Http.SecurityHeaders.Abstractions` (contracts for the CSP contributor pattern)

## Content-Security-Policy composition

The default CSP is API-grade strict:

```text
default-src 'none'; base-uri 'none'; frame-ancestors 'none'
```

Other Granit packages that mount UI surfaces (e.g. Scalar in
`Granit.Http.ApiDocumentation`) declare their own `ICspContributor` that
relaxes the policy for their endpoints only. The composer runs all
matching contributors per request and emits a single, per-endpoint CSP
header.

To disable a contributor (e.g. when an internal security policy is stricter
than the framework default), name it in `appsettings.json`:

```json
{
  "SecurityHeaders": {
    "DisabledContributors": ["ScalarCspContributor"]
  }
}
```

To override the entire policy with a literal string (emergency only):

```json
{
  "SecurityHeaders": {
    "Csp": { "RawOverride": "default-src 'self'" }
  }
}
```

`RawOverride` bypasses composition and every contributor. Setting it logs a
warning at startup so the override doesn't outlive its emergency.

### Trap: external CSP headers are overwritten

The framework owns the `Content-Security-Policy` response header. Any
`[ResponseHeader("Content-Security-Policy", ...)]` attribute, custom
middleware, or output-cache layer that writes the CSP will be silently
overwritten by the composer. This is intentional: browsers intersect
multiple CSP headers and pick the strictest combination, which would
silently neutralise contributor relaxations. If you need full control, set
`CspOptions.RawOverride`.

### `'unsafe-inline'` × nonce interaction

Per CSP spec, a nonce or hash source suppresses `'unsafe-inline'`. If a
contributor adds both into the same directive, the runtime emits a
`LogWarning` once per affected endpoint. Migrate to nonce-only and remove
the `'unsafe-inline'` source.

## Documentation

See the [full documentation](https://granit-fx.dev).
