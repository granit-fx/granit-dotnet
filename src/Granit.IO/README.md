# Granit.IO

Secure temp-file primitive for the Granit framework.

## What it provides

- `ITempFileFactory.CreateAsync(category, extension)` returns an `ITempFile`
  wrapping a hardened file handle:
  - POSIX `0600` on Linux/macOS (file) and `0700` on the containing directory
  - NTFS ACL restricted to the current user on Windows
  - `FileOptions.DeleteOnClose` — disposing the stream removes the file
  - Tenant-partitioned directory layout (`{root}/t-{tenantId}/{category}/...`)
  - `LimitedStream` cap (256 MiB default) — throws `IOException` on overflow
- `TempFileJanitor` background service: sweeps `RootDirectory` every
  `JanitorInterval` (5 min default) and deletes any file older than
  `MaxLifetime` (1 h default).
- `IoMetrics` (`Granit.IO` meter):
  - `granit.io.temp.created`
  - `granit.io.temp.deleted`
  - `granit.io.temp.bytes` (histogram)
  - `granit.io.temp.janitor.purged`

## Standards

OWASP ASVS V12.4.1 (untrusted files), ISO 27001 A.5.34 (privacy + PII at rest),
GDPR Art. 32 (security of processing).

## Consumers

- `Granit.Browsing` — HAR, trace, and PDF-viewer artefacts.
- `Granit.DataExchange` — export staging files.
- `Granit.Privacy` — GDPR data-subject export ZIPs.
