# Granit.BlobStorage

File storage module for Granit. Direct-to-Cloud architecture with Pre-signed URLs (S3), multi-tenant isolation, post-upload validation pipeline, and GDPR Crypto-Shredding.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BlobStorage
```

## Dependencies

- `Granit`
- `Granit.Guids`

## Module System

This core module performs no storage on its own. It is pulled into the module
graph transitively by `Granit.BlobStorage.Endpoints` and any provider package
(e.g. `Granit.BlobStorage.S3`) via their `[DependsOn]` declarations, so consumers
of either get it automatically. Only if you use neither — wiring the core SDK
directly — add `[DependsOn(typeof(GranitBlobStorageModule))]` to your application
module. In all cases pair it with a provider package (S3, Azure Blob, file
system, …); the core module alone stores nothing.

## Documentation

See the [full documentation](https://granit-fx.dev).
