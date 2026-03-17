# Granit.Encryption.ReEncryption

Re-encryption job for Granit field-level encryption. Iterates entities with
`[Encrypted]` properties and forces re-encryption to the current key version,
enabling safe key rotation without downtime.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Encryption.ReEncryption
```

## Dependencies

- `Granit.Encryption.EntityFrameworkCore`

## Documentation

See the [full documentation](https://granit-fx.dev).
