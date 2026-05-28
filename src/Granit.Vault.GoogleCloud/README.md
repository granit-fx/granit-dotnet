# Granit.Vault.GoogleCloud

Google Cloud KMS and Secret Manager provider for `Granit.Vault`. Registered as Keyed Service with key `"GoogleCloud"` for multi-provider resolution.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault.GoogleCloud
```

## Features

- **KMS transit encryption**: `ITransitEncryptionService` backed by Cloud KMS symmetric encryption
- **Cloud KMS HMAC**: `ITransitMacService` via `MacSign` / `MacVerify` on a CryptoKey with `purpose=MAC, algorithm=HMAC_SHA256`. Version-state aware — refuses verification against `DISABLED` / `DESTROYED` versions.
- **Secret Manager**: `IDatabaseCredentialProvider` with automatic rotation detection
- **Health check**: Cloud KMS key reachability probe

### MAC configuration

```jsonc
{
  "Vault": {
    "GoogleCloud": {
      "ProjectId": "granit-prod", "Location": "europe-west1", "KeyRing": "granit-privacy",
      "CryptoKey": "granit-encryption",
      "Mac": { "CryptoKeyId": "granit-privacy-export-mac" }
    }
  }
}
```

Provision the MAC CryptoKey via Terraform with `purpose = "MAC"`, `algorithm = "HMAC_SHA256"`, and `rotation_period = "7776000s"` (90 days, ISO 27001 A.10.1.2).

## Dependencies

- `Granit.Vault`

## Documentation

See the [full documentation](https://granit-fx.dev).
