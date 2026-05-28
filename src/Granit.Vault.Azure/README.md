# Granit.Vault.Azure

Azure Key Vault provider for Granit applications: transit encryption, database credential rotation, and string encryption.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault.Azure
```

## Features

- **Transit encryption**: `ITransitEncryptionService` backed by Azure Key Vault RSA keys
- **Managed HSM HMAC**: `ITransitMacService` via `HS256` on `oct-HSM` keys (premium tier only)
- **Secret rotation**: `IDatabaseCredentialProvider` with automatic rotation detection
- **String encryption**: `IStringEncryptionProvider` for column-level data encryption
- **Health check**: Key Vault key reachability probe
- **Managed Identity**: `DefaultAzureCredential` (Managed Identity on AKS/App Service, Azure CLI for local dev)

### MAC on Standard tier

Azure Key Vault **Standard** tier exposes no HMAC primitive. For Standard-tier hosts,
register the portable secret-backed fallback from `Granit.Vault` instead — the key
material is stored in a Key Vault secret and HMAC is computed locally:

```csharp
services.AddGranitSecretBackedMacService(o =>
{
    o.CurrentSecretName  = "granit-mac-current";
    o.PreviousSecretName = "granit-mac-previous";
});
```

### MAC on Managed HSM (premium)

```csharp
services.AddGranitVaultAzureManagedHsmMac();
```

```jsonc
{
  "Vault": { "Azure": { "ManagedHsm": { "Mac": {
    "HsmUri":  "https://my-hsm.managedhsm.azure.net/",
    "KeyName": "granit-privacy-export-mac"
  }}}}
}
```

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
