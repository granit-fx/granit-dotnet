# Granit.IpGeolocation

Provider-agnostic IP geolocation for `granit`. Resolves an IP address to an
approximate `GeoLocation` (city / region / country / coordinates) through an
ordered chain of pluggable providers, with result caching and a privacy-first
no-op default.

Part of the [granit](https://granit-fx.dev) framework.

## How it works

`IIpGeolocationResolver` is the entry point. It short-circuits absent, private,
loopback, and unparseable addresses to `null`, serves results from
`Granit.Caching` (`IFusionCache`), and otherwise walks the configured provider
chain until one yields a location. A failing provider is logged (with a **masked**
IP) and skipped, so resolution never throws — with no provider installed it is a
silent no-op returning `null`.

```text
ip ─▶ resolver ─▶ [private/parse check] ─▶ [cache] ─▶ provider₁ ─▶ provider₂ ─▶ … ─▶ GeoLocation?
```

Results are cached through `Granit.Caching`, so installing a distributed cache
(`Granit.Caching.StackExchangeRedis`) makes lookups cluster-shared automatically —
a single resolution serves the whole fleet.

## Registration

```csharp
// Program.cs — the core module is pulled in by any provider package.
builder.AddGranitIpGeolocationMaxMind();   // offline .mmdb provider (recommended)
builder.AddGranitIpGeolocationIpInfo();     // opt-in third-party API provider
```

```json
{
  "IpGeolocation": {
    "ProviderOrder": [ "MaxMind", "IpInfo" ],
    "CacheDuration": "01:00:00"
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `ProviderOrder` | `[]` (all, registration order) | Provider fallback order, by name |
| `CacheDuration` | `01:00:00` | TTL for cached results (incl. negatives) |
| `ResolvePrivateAddresses` | `false` | Resolve private/loopback addresses (testing only) |
| `CacheKeySecret` | *(none)* | Optional secret keying the cache-key hash (HMAC). Set it (from Vault) to stop a cache dump being brute-forced back to IPs |

## Privacy (GDPR)

- An IP address is personal data. Prefer the **offline** `Granit.IpGeolocation.MaxMind`
  provider, which keeps resolution on-premise.
- Third-party API providers transmit the IP to a sub-processor and are **opt-in**.
- Raw IPs are never logged — failures log a masked form. Use `IpMasking.Mask` to
  derive a host-zeroed address for any value exposed to a client.
- Cache keys hash the IP so no raw address reaches a shared cache. Set
  `CacheKeySecret` (from Vault) to key that hash (HMAC) — otherwise a plain
  SHA-256 over the IPv4 space is enumerable offline from a cache dump.

## Providers

| Package | Kind | Notes |
| ------- | ---- | ----- |
| `Granit.IpGeolocation.MaxMind` | Offline `.mmdb` | MaxMind GeoLite2/GeoIP2 + DB-IP |
| `Granit.IpGeolocation.IpInfo` | Third-party API | ipinfo.io — opt-in |

## Dependencies

- `Granit` (module system)
- `Granit.Caching` (`IFusionCache` result cache)

## License

Apache-2.0
