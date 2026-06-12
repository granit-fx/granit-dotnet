# Granit.IpGeolocation.IpInfo

Opt-in third-party IP geolocation provider for `Granit.IpGeolocation`, backed by
the [ipinfo.io](https://ipinfo.io) HTTP API.

Part of the [granit](https://granit-fx.dev) framework.

> **GDPR.** This provider transmits the client IP to an external processor. It is
> only active when explicitly registered **and** listed in `IpGeolocation:ProviderOrder`.
> For privacy-sensitive deployments, prefer the offline `Granit.IpGeolocation.MaxMind`
> provider and keep this as a fallback (or omit it).

## How it works

The provider calls `GET {BaseAddress}/{ip}/json`, sending the API token as a
`Bearer` header (never in the URL). Failures are caught internally and logged with a
failure category only — never the request URI. Because the URI carries the IP in its
path, the default `HttpClient` factory logging (which records the URI at `Information`)
is removed for this client, and the redaction handler scrubs the IP from the outbound
trace span. The `Granit.IpGeolocation` resolver caches the result, so an installed
distributed cache means one API call per IP per cluster.

## Registration

```csharp
// Program.cs
builder.AddGranitIpGeolocationIpInfo();
```

```json
{
  "IpGeolocation": {
    "ProviderOrder": [ "MaxMind", "IpInfo" ],
    "IpInfo": {
      "ApiToken": "<ipinfo-token>",
      "BaseAddress": "https://ipinfo.io",
      "Timeout": "00:00:03"
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `ApiToken` | *(none)* | ipinfo.io token (sent as `Bearer`) |
| `BaseAddress` | `https://ipinfo.io` | API base URL (must be http/https) |
| `Timeout` | `00:00:03` | Per-request timeout |
| `MaxResponseSizeBytes` | `65536` | Cap on the buffered response body (untrusted egress input) |
| `ProviderName` | `IpInfo` | Name used in `ProviderOrder` |

## Security

- Auto-redirects are disabled on the primary handler (closes a redirect-based SSRF path).
- The IP is re-validated as a bare literal before being placed in the request path,
  so it cannot override the configured base address.
- The token is sent as a header, not a query parameter.
- The IP is stripped from the outbound HTTP trace (`url.full` / `url.path`) by a
  redaction handler, so it does not leak into trace exporters.
- The default `HttpClient` request logging is removed for this client, so the IP-bearing
  request URI is never written to the log pipeline (only trace and logs are both covered).
- The response body is size-capped (`MaxResponseSizeBytes`) — a third-party response
  is untrusted input re-entering the process.
- **Cost / denial-of-wallet:** each distinct public IP triggers one API call before
  the result is cached. List `MaxMind` first in `ProviderOrder` so the offline lookup
  absorbs most traffic, and apply a host-level outbound rate limiter if request
  cardinality is attacker-influenced (e.g. an untrusted `X-Forwarded-For`).

## Dependencies

- `Granit.IpGeolocation` (core abstractions)
- `Microsoft.Extensions.Http`

## License

Apache-2.0
