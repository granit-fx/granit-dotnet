# Granit.Vault.Azure

Azure Key Vault provider for Granit applications: transit encryption, database credential rotation, and string encryption.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault.Azure
```

## Features

- **Transit encryption**: `ITransitEncryptionService` backed by Azure Key Vault RSA keys
- **Secret rotation**: `IDatabaseCredentialProvider` with automatic rotation detection
- **String encryption**: `IStringEncryptionProvider` for column-level data encryption
- **Health check**: Key Vault key reachability probe
- **Managed Identity**: `DefaultAzureCredential` (Managed Identity on AKS/App Service, Azure CLI for local dev)

## Configuration

```json
{
  "Vault": {
    "Azure": {
      "VaultUri": "https://my-vault.vault.azure.net/",
      "EncryptionKeyName": "granit-encryption",
      "EncryptionAlgorithm": "RSA-OAEP-256",
      "DatabaseSecretName": "db-credentials"
    }
  },
  "Encryption": {
    "ProviderName": "AzureKeyVault"
  }
}
```

## Dependencies

- `Granit.Vault` (abstractions)

## Documentation

See the [full documentation](https://granit-fx.dev).
