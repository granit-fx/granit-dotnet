# Granit.Http.SecurityHeaders.Endpoints

Audit endpoint for the Granit CSP composition surface. Exposes a single
`GET /security-headers/csp` endpoint that returns the effective per-route
`Content-Security-Policy` snapshot — base directives, registered
contributors, and composed CSP per matched endpoint — so security auditors
can verify the policy from outside the deployment.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.SecurityHeaders.Endpoints
```

Then in `Program.cs`:

```csharp
app.MapGranitSecurityHeadersAudit();
```

The endpoint is gated by `DiagnosticsPermissions.Monitoring.Read` (same as
the rest of the diagnostics surface — no new permission to localise).

## Caveat

Contributors that branch on request-scoped state beyond
`Endpoint.Metadata` (e.g. authenticated user, tenant, headers) will
**under-report** in the audit response — the audit synthesises requests
carrying only the matched endpoint. The audit reflects "what would be
applied on a synthetic request to this route", not "what every real
request will see". Per framework convention, contributors MUST be pure
functions of the matched endpoint, so this caveat is theoretical.

## Documentation

See the [full documentation](https://granit-fx.dev).
