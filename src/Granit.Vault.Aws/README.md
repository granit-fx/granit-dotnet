# Granit.Vault.Aws

AWS KMS and Secrets Manager provider for Granit applications: transit encryption, database credential rotation, and string encryption.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault.Aws
```

## Features

- **KMS transit encryption**: `ITransitEncryptionService` backed by AWS KMS symmetric encryption
- **Secrets Manager**: `IDatabaseCredentialProvider` with automatic rotation detection
- **String encryption**: `IStringEncryptionProvider` for column-level data encryption
- **Health check**: KMS key reachability probe
- **IAM roles**: Default credential chain (ECS/EKS), access keys for local dev

## Configuration

```json
{
  "Vault": {
    "Aws": {
      "Region": "eu-west-1",
      "KmsKeyId": "alias/granit-encryption",
      "DatabaseSecretArn": "arn:aws:secretsmanager:eu-west-1:123456789:secret:db-creds"
    }
  },
  "Encryption": {
    "ProviderName": "AwsKms"
  }
}
```

## Dependencies

- `Granit.Vault` (abstractions)

## Documentation

See the [full documentation](https://granit-fx.dev).
