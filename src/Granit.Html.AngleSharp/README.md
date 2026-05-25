# Granit.Html.AngleSharp

AngleSharp-backed implementation of the `Granit.Html` abstractions. Ships
`AngleSharpHtmlToPlainTextConverter` (consumed by `Granit.Notifications.Email` and
future `Granit.TextExtraction.Text`) and an `AngleSharpConfiguration` factory that
keeps trusted-template vs untrusted-content profiles strictly separated.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Html.AngleSharp
```

## Dependencies

- `Granit`
- `Granit.Html`
- `AngleSharp`

## Why two configuration profiles?

AngleSharp can resolve external resources (linked CSS, images, scripts) when
configured with `WithDefaultLoader()`. That is **safe for trusted email templates**
the framework ships itself — but **dangerous for untrusted user input** since it
opens an SSRF channel. `AngleSharpConfiguration` exposes two factory methods to make
the choice explicit at the call site:

- `BuildForTrustedTemplates()` — current Notifications.Email behaviour.
- `BuildForUntrustedContent()` — no external resource resolution. Use this for
  anything that didn't come from a Granit template the host controls.

An architecture test in `Granit.Html.AngleSharp.Tests` verifies the source code
never accidentally calls `WithDefaultLoader()`.

## Documentation

See the [full documentation](https://granit-fx.dev).
