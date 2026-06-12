# Granit.IpGeolocation.MaxMind

Offline IP geolocation provider for `Granit.IpGeolocation`, backed by a MaxMind
DB format (`.mmdb`) database. Reads MaxMind GeoLite2 / GeoIP2 (City or Country)
and DB-IP Lite databases. Lookups are fully local — no data leaves the host.

Part of the [granit](https://granit-fx.dev) framework.

## How it works

The `.mmdb` file is opened entirely in managed memory (`FileAccessMode.Memory`),
so no OS file handle is retained and a scheduled database update can replace the
file without an "in use" error. With `ReloadOnChange` (default), the file is
watched and a fresh reader is atomically swapped in when it changes — no restart
needed.

The database file is **supplied by the consumer** and is large and separately
licensed; this package never embeds or redistributes it.

```text
GeoLite2-City.mmdb ──(in-memory)──▶ DatabaseReader ──▶ GeoLocation
        ▲ weekly cron swap → FileSystemWatcher → atomic reader reload
```

## Registration

```csharp
// Program.cs
builder.AddGranitIpGeolocationMaxMind();
```

```json
{
  "IpGeolocation": {
    "ProviderOrder": [ "MaxMind" ],
    "MaxMind": {
      "DatabasePath": "/var/lib/geoip/GeoLite2-City.mmdb"
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `DatabasePath` | *(required)* | Path to the `.mmdb` file |
| `ProviderName` | `MaxMind` | Name used in `ProviderOrder` |
| `ReloadOnChange` | `true` | Hot-reload the database when the file changes |
| `FileAccess` | `Memory` | `Memory` (no lock) or `MemoryMapped` |

## Provisioning the database

Download a `.mmdb` from MaxMind (GeoLite2 — free, account required) or DB-IP
(DB-IP Lite — free) and place it at `DatabasePath`. Keep it fresh with a periodic
update job; the provider hot-reloads on change.

## Licensing

The MaxMind GeoLite2 databases are distributed under the MaxMind EULA and require
CC BY-SA 4.0 attribution; DB-IP Lite is CC BY 4.0. These apply to the **database
file you provision**, not this package. See the repository `THIRD-PARTY-NOTICES.md`.

## Dependencies

- `Granit.IpGeolocation` (core abstractions)
- `MaxMind.GeoIP2`

## License

Apache-2.0
