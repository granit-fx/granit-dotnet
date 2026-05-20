# Granit.Caching.Vault

Bridges [`ISecretStore`](https://granit-fx.dev) (Granit.Vault) to
`Cache:Encryption:Key` (Granit.Caching). Hydrates the AES-256 key at host start
via `IPostConfigureOptions<CacheEncryptionOptions>`, fail-closed if Vault is
unreachable or encryption is disabled.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Caching.Vault
```

## Dependencies

- `Granit.Caching`
- `Granit.Vault` (+ a provider: `.HashiCorp`, `.Azure`, `.Aws`, `.GoogleCloud`)

## Documentation

See the [full documentation](https://granit-fx.dev).
