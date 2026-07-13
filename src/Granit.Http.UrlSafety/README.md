# Granit.Http.Security

Shared HTTP-layer security primitives extracted out of `Granit.Browsing` in the
pre-1.0 hardening pass (ADR-055, #1964): URL safety classification + DNS / IP
guards consumable by any module that takes user-controlled URLs.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Http.Security
```

## What it does

- `IUrlSafetyValidator` — central SSRF / private-network / reserved-TLD guard.
  Used by `Granit.Browsing` (page navigation, request routing), `Granit.Webhooks`
  (outbound webhook delivery), and any other module taking outbound URLs from
  the host application.
- `PrivateNetworkClassifier` — RFC 1918 / RFC 4193 / link-local / loopback
  detection.
- `ReservedTldClassifier` — refuses `.local`, `.localhost`, `.test`, `.example`,
  `.invalid`.
- Localised violation messages (`UrlSafetyViolation` + `UrlSafetyViolationKind`)
  for all 18 cultures.

## Usage

```csharp
builder.AddGranitHttpSecurity(opts =>
{
    opts.AllowedSchemes = ["https"];
    opts.BlockPrivateNetworks = true;
    opts.BlockReservedTlds = true;
});

// Then inject IUrlSafetyValidator anywhere a URL crosses the trust boundary.
```

## Related

- [ADR-055 — Extract URL safety and temp-file primitives](https://granit-fx.dev/dotnet/architecture/adr/055-extract-url-safety-and-temp-files/)
- [Browsing security](https://granit-fx.dev/dotnet/browsing/security/)
