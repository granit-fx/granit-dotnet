# Granit.TextExtraction.Email

RFC822 email (`.eml`) text extractor for the
[`Granit.TextExtraction`](../Granit.TextExtraction/README.md) pipeline. Backed
by [MimeKit](https://github.com/jstedfast/MimeKit) — pure managed code, no
native dependencies. Reuses
[`Granit.Html.AngleSharp`](../Granit.Html.AngleSharp/README.md) for the
SSRF-safe HTML-body fallback.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.TextExtraction.Email
```

## Dependencies

- `Granit.TextExtraction`
- `Granit.Html.AngleSharp`
- `MimeKit`

## Content types

- `message/rfc822` (primary)
- `application/eml` (alias emitted by some clients)

## Extraction shape

The extractor emits a structured header summary followed by the body text:

```text
Subject: <subject>
From: <addresses>
To: <addresses>
Cc: <addresses>            # only when present
Date: <RFC 1123, UTC>

<body>
```

Body resolution:

- Prefers `MimeMessage.TextBody` (the `text/plain` alternative) when present.
- Falls back to `MimeMessage.HtmlBody` routed through an
  `IHtmlToPlainTextConverter` built from
  `AngleSharpConfiguration.BuildForUntrustedContent()` — no external resource
  loader (SSRF guard).

## Out of scope (by design)

- **Attachments** — the envelope alone covers the bulk of search relevance for
  email. Attachment payloads, if persisted to blob storage, are picked up by
  their own per-format extractors downstream.
- **Decryption** — `S/MIME` and `PGP` encrypted bodies degrade to a literal
  `[encrypted message]` placeholder. The extractor is a search-indexer, not
  a mail user agent.
- **`.msg` (Outlook binary)** — separate format, separate package. The Tika
  sidecar (`Granit.TextExtraction.Tika`) covers it in the meantime.

## Security posture

- Input wrapped in `LimitedStream` (`MaxBodySizeBytes` cap) before any
  MimeKit allocation.
- Header values are sanitised — control characters (`< 0x20`, except `\n`
  and `\t`) are stripped to prevent log/header injection through indexed
  content (an attacker crafting a `Subject` with embedded CR/LF cannot splice
  fake log lines downstream).
- HTML body path uses `AngleSharpConfiguration.BuildForUntrustedContent()`
  — no default loader, no SSRF channel.
- Malformed `.eml` payloads (`FormatException`, `EndOfStreamException`) are
  soft-skipped: empty content + `IsTruncated = true`, no exception
  propagated. The body cap is hard: breaching it raises
  `TextExtractionException("input_too_large")`.

## Third-party licenses

- **MimeKit** — MIT (Jeffrey Stedfast). Already listed in
  [`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md) (consumed by
  `Granit.Notifications.Smtp`).

## Documentation

See the [full documentation](https://granit-fx.dev).
