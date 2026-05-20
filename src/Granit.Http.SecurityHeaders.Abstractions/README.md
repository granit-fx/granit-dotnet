# Granit.Http.SecurityHeaders.Abstractions

Contracts for composing the `Content-Security-Policy` across Granit packages.

Reference this package — not `Granit.Http.SecurityHeaders` — when a framework
module needs to declare a per-endpoint CSP relaxation (e.g. for a Scalar UI, a
Hangfire-like dashboard, a Razor login form). The runtime package is optional
from the consumer's perspective: if it is absent, contributor registrations
no-op cleanly. Mirrors the `Microsoft.Extensions.Logging.Abstractions` split.

## What's in the box

- `ICspContributor` — interface implemented by packages that need to layer
  sources onto the base CSP for a specific endpoint.
- `ICspContributorRegistry` — singleton registry populated at app
  configuration time. Locks on first response composition.
- `CspBuilder` — per-request fluent collector. Contributors call
  `builder.AddScriptSrc("'self'", ...)`; the composer reads back the state.

Full conventions: <https://granit-fx.dev/dotnet/infrastructure/http/security-headers/>.
