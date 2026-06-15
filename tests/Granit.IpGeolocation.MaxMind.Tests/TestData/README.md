# MaxMind test databases

`GeoIP2-City-Test.mmdb` and `GeoLite2-Country-Test.mmdb` are MaxMind's official
sample databases, vendored verbatim from
[`maxmind/MaxMind-DB`](https://github.com/maxmind/MaxMind-DB/tree/main/test-data).

They contain a small set of synthetic, well-known test IP ranges (e.g.
`2.125.160.216` → Boxford, GB; `81.2.69.160` → London, GB) and are used purely to
exercise `MaxMindDatabaseProvider` lookups offline. They are **not** production
data and are not redistributed in any Granit NuGet package, so they are out of
scope for `THIRD-PARTY-NOTICES.md` (which tracks production dependencies only).

Copyright (c) 2013–2026 MaxMind, Inc. Licensed under the
[Apache License 2.0](https://github.com/maxmind/MaxMind-DB/blob/main/LICENSE-APACHE)
or the [MIT License](https://github.com/maxmind/MaxMind-DB/blob/main/LICENSE-MIT),
at your option — compatible with Granit's Apache-2.0 license.
