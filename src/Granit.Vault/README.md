# Granit.Vault

Vault abstractions for Granit applications: transit encryption, dynamic database credentials, and string encryption interfaces.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Vault
```

## Abstractions

| Interface | Purpose |
| --------- | ------- |
| `ITransitEncryptionService` | Encrypt/decrypt byte arrays via a transit engine |
| `IDatabaseCredentialProvider` | Dynamic database credentials with automatic rotation |
| `IStringEncryptionProvider` | Column-level string encryption (from `Granit.Encryption`) |

## Providers

Install a provider package to get a concrete implementation:

| Provider | Package |
| -------- | ------- |
| HashiCorp Vault | `Granit.Vault.HashiCorp` |
| Azure Key Vault | `Granit.Vault.Azure` |
| AWS KMS + Secrets Manager | `Granit.Vault.Aws` |

## Dependencies

- `Granit.Encryption`

## Documentation

See the [full documentation](https://granit-fx.dev).
