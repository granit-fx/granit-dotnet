# Granit.Privacy.Vault

Vault-backed `IExportHmacSigner` and `IExportContentSigner` for
[`Granit.Privacy`](https://granit-fx.dev). Replaces the in-process
`EphemeralExportHmacSigner` (development-only) with a shared-state, restart-surviving
signer driven by any registered `ITransitMacService`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Privacy.Vault
```

This package depends on `Granit.Vault` (abstractions) only — the actual MAC provider
is selected by whichever Vault provider module the host installs:

- `Granit.Vault.HashiCorp` (Transit HMAC)
- `Granit.Vault.Aws` (KMS HMAC)
- `Granit.Vault.GoogleCloud` (Cloud KMS MAC)
- `Granit.Vault.Azure` (Managed HSM HS256)
- `Granit.Vault` ➜ `AddGranitSecretBackedMacService(...)` (portable fallback)

## Why

`Granit.Privacy.BlobStorage` ships `EphemeralExportHmacSigner` as the default
`IExportHmacSigner` / `IExportContentSigner`. That signer:

- Generates a fresh 256-bit key on every process start (logged as a `Warning`).
- `EphemeralExportHmacSignerStartupGuard` refuses any non-`Development` environment.

`VaultExportHmacSigner` lets production hosts swap the ephemeral key for a key that
lives in a vault and survives restarts and replica scale-out.

## Usage

```csharp
// 1. Install a Vault provider — pick one, registers ITransitMacService.
context.Services.AddGranitVaultHashiCorp();

// 2. Wire the privacy-export signer.
context.Services.AddGranitPrivacyVaultExportSigner(o =>
{
    o.FragmentKeyName = "granit-privacy-export-fragment-mac";
    o.ContentKeyName  = "granit-privacy-export-content-mac";
});
```

Two distinct vault keys: usage separation per ISO 27001 A.10.1 — fragment-identity
tags and arbitrary manifest payloads should never share signing material.

## Tag format

```
gpv1:{providerOpaqueTag}
```

The `gpv1:` framework prefix is stripped before delegating to the underlying
`ITransitMacService`. Tags produced by the legacy `EphemeralExportHmacSigner`
(`vN:base64`) are explicitly rejected — operators must drain in-flight exports
before swapping signers (no auto-migration window).

## Showcase wiring

`Showcase.Host` registers the HashiCorp Vault provider in non-Development environments
and calls `AddGranitPrivacyVaultExportSigner` — the
`EphemeralExportHmacSignerStartupGuard` then accepts the boot (the replacement
`IExportHmacSigner` is `VaultExportHmacSigner`, not `EphemeralExportHmacSigner`).

## Dependencies

- `Granit.Privacy`
- `Granit.Privacy.BlobStorage`
- `Granit.Vault`

## Documentation

See the [full documentation](https://granit-fx.dev).
