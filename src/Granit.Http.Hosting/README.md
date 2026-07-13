# Granit.Http.Hosting

Host-facing HTTP pipeline defaults for Granit applications — the thin wrappers every
web host wants, consolidated in one package (formerly `Granit.Http.Cors` and
`Granit.Http.ResponseCompression`).

## CORS (`Http:Cors`)

- Default policy driven by `GranitCorsOptions.AllowedOrigins` (trailing-slash
  normalization, per-origin format validation).
- ISO 27001: wildcard origins rejected outside Development; wildcard + credentials
  rejected always.
- Middleware auto-applied at the head of the pipeline (`AutoRegisterMiddleware`,
  default `true`) — forgetting `UseCors()` is no longer a silent no-op.

## Response compression (`Http:ResponseCompression`)

- Brotli + gzip, HTTPS-on defaults.
- `text/event-stream` exclusion is non-overridable (SSE must never buffer).
- BREACH/CRIME considerations documented on the options.

Configuration sections keep their historical names — they name the feature, not the
package.

## Documentation

See the [full documentation](https://granit-fx.dev).
