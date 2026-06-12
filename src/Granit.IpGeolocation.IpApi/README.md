# Granit.IpGeolocation.IpApi

Opt-in third-party IP geolocation provider for `Granit.IpGeolocation`, backed by
the [ipinfo.io](https://ipinfo.io) HTTP API.

Part of the [granit](https://granit-fx.dev) framework.

> **GDPR.** This provider transmits the client IP to an external processor. It is
> only active when explicitly registered **and** listed in `IpGeolocation:ProviderOrder`.
> For privacy-sensitive deployments, prefer the offline `Granit.IpGeolocation.MaxMind`
> provider and keep this as a fallback (or omit it).

## How it works

The provider calls `GET {BaseAddress}/{ip}/json`, sending the API token as a
`Bearer` header (never in the URL). Failures are caught internally and the request
URI — which contains the IP — is never logged; only a failure category is. The
`Granit.IpGeolocation` resolver caches the result, so an installed distributed
cache means one API call per IP per cluster.

## Registration

```csharp
// Program.cs
builder.AddGranitIpGeolocationIpApi();
```

```json
{
  "IpGeolocation": {
    "ProviderOrder": [ "MaxMind", "IpApi" ],
    "IpApi": {
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
| `ProviderName` | `IpApi` | Name used in `ProviderOrder` |

## Security

- Auto-redirects are disabled on the primary handler (closes a redirect-based SSRF path).
- The token is sent as a header, not a query parameter.
- Note: outbound HTTP instrumentation may record the request URL (with the IP);
  the offline provider avoids this entirely.

## Dependencies

- `Granit.IpGeolocation` (core abstractions)
- `Microsoft.Extensions.Http`

## License

Apache-2.0
