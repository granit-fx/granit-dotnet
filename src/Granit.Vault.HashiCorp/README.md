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

```json
{
  "Vault": {
    "Address": "https://vault.example.com",
    "Token": "...",
    "TransitKeyName": "granit-encryption",
    "DatabaseRoleName": "granit-db"
  }
}
```

## Dependencies

- `Granit.Vault` (abstractions)
- `VaultSharp`

## Documentation

See the [full documentation](https://granit-fx.dev).
