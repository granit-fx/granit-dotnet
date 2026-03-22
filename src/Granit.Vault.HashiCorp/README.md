# Granit.Vault.HashiCorp

HashiCorp Vault provider for Granit applications: Transit encryption, dynamic database credentials, and string encryption.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault.HashiCorp
```

## Features

- **Transit encryption**: `ITransitEncryptionService` backed by HashiCorp Vault Transit engine
- **Dynamic credentials**: `IDatabaseCredentialProvider` with automatic lease renewal
- **String encryption**: `IStringEncryptionProvider` for column-level data encryption
- **Health check**: Vault seal status and Transit key reachability probe

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
