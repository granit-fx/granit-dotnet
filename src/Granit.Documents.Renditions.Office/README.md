# Granit.Documents.Renditions.Office

Office (`docx` / `xlsx` / `pptx` + legacy `doc` / `xls` / `ppt` + ODF + RTF) →
`application/pdf` rendition provider for `Granit.Documents.Renditions` (F16.7).
Drives LibreOffice headless (`soffice --headless --convert-to pdf`) through a
serialised process pool.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions.Office
```

The framework does NOT bundle the LibreOffice binary — install it on the runtime image:

```bash
# Debian / Ubuntu base
apt-get update && apt-get install -y --no-install-recommends libreoffice

# Alpine base
apk add --no-cache libreoffice

# macOS dev box
brew install --cask libreoffice
```

## What it does

- Registers `OfficeRenditionProvider` (`Office → application/pdf`).
- Combined with `Granit.Documents.Renditions.Pdf` and `.Imaging`, the pipeline
  solver can reach an image target in 3 hops: `docx → pdf → png → webp`.
- Each conversion runs in a per-invocation temp directory under
  `{temp}/granit-soffice-{guid}` with its own `-env:UserInstallation` profile,
  so the directory is cleaned up regardless of outcome.
- Invocations are serialised through a `SemaphoreSlim` sized by
  `OfficeRenditionOptions.MaxConcurrentConversions` (default 1). LibreOffice
  headless is not thread-safe against a shared user profile.

## Configuration

| Option | Default | Effect |
| --- | --- | --- |
| `Documents:Renditions:Office:SofficeBinary` | `soffice` | Path to the `soffice` binary. Absolute path required if not on `PATH`. |
| `Documents:Renditions:Office:MaxConcurrentConversions` | `1` | Max concurrent `soffice` invocations. Each worker needs its own user profile. |
| `Documents:Renditions:Office:ConversionTimeout` | `00:01:00` | Hard timeout per conversion. |
| `Documents:Renditions:Office:UserProfileDirectoryTemplate` | `null` | Override the per-invocation profile location. `{guid}` substitution is supported. |

## Performance

LibreOffice cold-start is 2–3 seconds per invocation, so the **first** thumbnail
of an Office document carries that overhead. A warm-worker pool (long-lived
`soffice` daemon, UNO socket reuse) is on the roadmap for Phase 3 — not in scope
for this slice.

## License

LibreOffice is LGPL-licensed; only the binary is invoked, no LibreOffice code is
linked into the framework. The framework itself stays Apache-2.0.
