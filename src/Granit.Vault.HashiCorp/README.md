# Granit.Vault.HashiCorp

HashiCorp Vault provider for Granit applications: Transit encryption, dynamic database credentials, and string encryption.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault.HashiCorp
```

## Features

- **Transit encryption**: `ITransitEncryptionService` backed by HashiCorp Vault Transit engine
- **Transit MAC**: `ITransitMacService` via `transit/hmac` + `transit/verify` (SHA-256). Native versioning — rolling rotation honours Vault's `min_decryption_version`.
- **Dynamic credentials**: `IDatabaseCredentialProvider` with automatic lease renewal
- **String encryption**: `IStringEncryptionProvider` for column-level data encryption
- **Health check**: Vault seal status and Transit key reachability probe

### Provisioning the MAC key

```bash
vault write -f transit/keys/granit-privacy-export-fragment-hmac \
  type=hmac \
  auto_rotate_period=2160h   # 90 days, ISO 27001 A.10.1.2
```

App policy requires `update` on `transit/hmac/<key>` and `transit/verify/<key>`.

## Configuration

> **The module is disabled in Development.** `IsEnabled()` returns
> `!Environment.IsDevelopment()`, so Vault is **not** initialized at all when
> running locally — for local testing run in a non-Development environment or
> mock the Vault-backed services.

Production (Kubernetes auth — the default):

```json
{
  "Vault": {
    "Address": "https://vault.example.com",
    "AuthMethod": "Kubernetes",
    "KubernetesRole": "my-backend",
    "DatabaseRoleName": "readwrite"
  },
  "Encryption": {
    "ProviderName": "Vault",
    "VaultKeyName": "string-encryption"
  }
}
```

Notes:

- `AuthMethod` defaults to `"Kubernetes"` (with `KubernetesRole` /
  `KubernetesTokenPath`). Set `"AuthMethod": "Token"` with a `Token` value only
  for **local development** — `Token` is dev-only and must **never** appear in
  production config.
- There is **no** `TransitKeyName` option. The Transit key used for string
  encryption is `VaultKeyName` under the separate `Encryption` section
  (`StringEncryptionOptions`, default `"string-encryption"`), which also requires
  `ProviderName: "Vault"`.
- `DatabaseRoleName` lives in the `Vault` section; its default is `"readwrite"`
  (override only if your Vault database role differs).

## Dependencies

- `Granit.Vault` (abstractions)
- `VaultSharp`

## Documentation

See the [full documentation](https://granit-fx.dev).
