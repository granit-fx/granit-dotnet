# Granit.Wolverine.Encryption

Field-level encryption for Wolverine envelopes and sagas. Scans `System.Text.Json` type metadata for `[Encrypted]` string properties and applies a transparent encrypt/decrypt converter backed by `IStringEncryptionService`. Closes the cleartext-PII gap on the outbox / saga store for messages that travel between processes or across deployments.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Wolverine.Encryption
```

## Dependencies

- `Granit.Encryption`
- `Granit.Wolverine`

## Documentation

See the [full documentation](https://granit-fx.dev).
