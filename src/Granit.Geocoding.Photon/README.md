# Granit.Geocoding.Photon

Opt-in forward-geocoding provider for `Granit.Geocoding`, backed by the
[Photon](https://photon.komoot.io) `/api` endpoint (an OpenStreetMap geocoder by
komoot).

Part of the [granit](https://granit-fx.dev) framework.

> **GDPR.** This provider transmits the address to an external service. It is only
> active when explicitly registered **and** listed in `Geocoding:ProviderOrder`.

## How it works

Photon has no structured-address parameters, so the address components are joined
into a single free-text query:
`GET {BaseAddress}/api?q=street,+postalcode,+city,+country&limit=1`
over a plain `HttpClient`. The response is GeoJSON; the first feature's
`geometry.coordinates` (ordered **`[longitude, latitude]`** per the GeoJSON spec)
is parsed with `System.Text.Json` — **no external SDK**, so it adds no third-party
license. The `Granit.Geocoding` engine caches the result, so an installed
distributed cache means one API call per address per cluster.

Outbound requests are paced to `RateLimitPerSecond` (default **1 req/s**) by an
internal throttle.

## Registration

```csharp
// Program.cs
builder.AddGranitGeocodingPhoton();
```

```json
{
  "Geocoding": {
    "ProviderOrder": [ "Photon" ],
    "Photon": {
      "BaseAddress": "https://photon.komoot.io",
      "Language": "en",
      "RateLimitPerSecond": 1
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `BaseAddress` | `https://photon.komoot.io` | API base URL (https, or http loopback) |
| `Timeout` | `00:00:05` | Per-request timeout |
| `MaxResponseSizeBytes` | `262144` | Cap on the buffered response body |
| `Language` | *(none)* | Optional `lang` (e.g. `en`, `de`, `fr`) — text only, never the coordinate |
| `UserAgent` | *(none)* | Optional identifying agent string (courteous, not required) |
| `RateLimitPerSecond` | `1` | Max requests/second (raise only when self-hosting) |
| `ProviderName` | `Photon` | Name used in `ProviderOrder` |

## Usage policy

The public `photon.komoot.io` endpoint is free but **fair-use** — extensive usage
is throttled and availability is not guaranteed. The built-in throttle paces
requests to `RateLimitPerSecond` (default 1 req/s). [Self-hosting Photon](https://github.com/komoot/photon)
lets you raise the rate. Unlike Nominatim, Photon does not require a `User-Agent`,
but setting one is courteous.

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
