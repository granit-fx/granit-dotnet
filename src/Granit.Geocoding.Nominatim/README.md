# Granit.Geocoding.Nominatim

Opt-in forward-geocoding provider for `Granit.Geocoding`, backed by the
[OpenStreetMap Nominatim](https://nominatim.org) `/search` API.

Part of the [granit](https://granit-fx.dev) framework.

> **GDPR.** This provider transmits the address to an external service. It is only
> active when explicitly registered **and** listed in `Geocoding:ProviderOrder`.

## How it works

The provider issues a structured query
`GET {BaseAddress}/search?street=…&postalcode=…&city=…&country=…&format=jsonv2&limit=1`
over a plain `HttpClient` and parses the first result's `lat`/`lon` with
`System.Text.Json` — **no external SDK**, so it adds no third-party license. The
`Granit.Geocoding` engine caches the result, so an installed distributed cache
means one API call per address per cluster.

Outbound requests are paced to `RateLimitPerSecond` (default **1 req/s**, the limit
for the shared public server) by an internal throttle.

## Registration

```csharp
// Program.cs
builder.AddGranitGeocodingNominatim();
```

```json
{
  "Geocoding": {
    "ProviderOrder": [ "Nominatim" ],
    "Nominatim": {
      "UserAgent": "MyApp/1.0 (ops@example.com)",
      "BaseAddress": "https://nominatim.openstreetmap.org",
      "RateLimitPerSecond": 1
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `UserAgent` | *(none — required)* | Identifying agent string (Nominatim policy) |
| `BaseAddress` | `https://nominatim.openstreetmap.org` | API base URL (https, or http loopback) |
| `Timeout` | `00:00:05` | Per-request timeout |
| `MaxResponseSizeBytes` | `262144` | Cap on the buffered response body |
| `RateLimitPerSecond` | `1` | Max requests/second (raise only when self-hosting) |
| `ProviderName` | `Nominatim` | Name used in `ProviderOrder` |

## Usage policy

The public `nominatim.openstreetmap.org` endpoint requires:

- A genuine, identifying `User-Agent` (an app name and a contact) — **mandatory**,
  enforced by startup validation.
- At most **1 request/second** — enforced by the built-in throttle. Self-hosting an
  instance lets you raise `RateLimitPerSecond`.

See the [Nominatim usage policy](https://operations.osmfoundation.org/policies/nominatim/).

## Security

- Auto-redirects are disabled on the primary handler (closes a redirect-based SSRF path).
- The address is stripped from the outbound HTTP trace (`url.full` / `url.query`) by a
  redaction handler, so it does not leak into trace exporters.
- The default `HttpClient` request logging is removed for this client, so the
  address-bearing request URI is never written to the log pipeline.
- The response body is size-capped (`MaxResponseSizeBytes`) — a third-party response
  is untrusted input re-entering the process.

## Dependencies

- `Granit.Geocoding` (core engine + abstractions)
- `Microsoft.Extensions.Http`

## License

Apache-2.0
