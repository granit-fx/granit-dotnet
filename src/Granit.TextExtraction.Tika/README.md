# Granit.TextExtraction.Tika

Opt-in extractor that forwards bytes to an
[Apache Tika](https://tika.apache.org/) sidecar container over HTTP and reads
back plain text. Covers RTF, ODF, mailboxes, archives, and the long tail of
MIME types not handled by the native TE-F2 extractors (PDF, Office, HTML,
Markdown, plain text).

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction.Tika
```

## Dependencies

- `Granit.TextExtraction`
- `Microsoft.Extensions.Http`

## Sidecar deployment

```yaml
# docker-compose excerpt — bind to localhost only, expose cross-host via mTLS proxy.
tika:
  image: apache/tika:3.0.0-full
  ports:
    - "127.0.0.1:9998:9998"
```

## Configuration

`appsettings.json`:

```json
{
  "TextExtraction": {
    "Tika": {
      "Uri": "https://tika.internal.example/tika",
      "TimeoutSeconds": 30,
      "AllowedHosts": ["tika.internal.example"],
      "RequireMutualTls": true
    }
  }
}
```

## Security posture (VULN-102)

- **`AllowedHosts`** — explicit allowlist of hostnames the extractor will talk
  to. Empty allowlist + non-empty `Uri` → the module refuses to start with a
  clear error. No "trust whatever's wired" default.
- **`RequireMutualTls`** — when `true`, the extractor refuses to start unless
  the host's `IHttpClientFactory` registration for the `granit-tika` named
  client is wired with a client certificate. Out-of-the-box defaults to
  `true` in production. **Note (PR #2273):** the actual cert-loading from
  `Granit.Vault` lives in a follow-up — for now the host wires the cert
  via its own `ConfigurePrimaryHttpMessageHandler` callback. The extractor
  only enforces that the named client has a primary handler configured.
- **`LimitedStream`** caps the request body before upload.
- **Per-call timeout** via `HttpClient.Timeout`.
- Response stream caps the read at `maxCharLength` characters to prevent a
  malicious Tika instance from returning unbounded text.

## Documentation

See the [full documentation](https://granit-fx.dev).
