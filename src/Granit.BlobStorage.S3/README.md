# Granit.BlobStorage.S3

S3-compatible implementation for Granit.BlobStorage. Pre-signed URL generation via AWSSDK.S3, multi-tenant prefix isolation. Compatible with S3-compatible object storage and MinIO.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BlobStorage.S3
```

## Dependencies

- `Granit.BlobStorage`

## Configuration

Register the provider in your host (or infrastructure) module:

```csharp
context.Builder.AddGranitBlobStorageS3();
```

This binds `S3BlobOptions` from the `BlobStorage` configuration section and
validates it on startup, so the section is **mandatory**:

```json
{
  "BlobStorage": {
    "ServiceUrl": "https://s3.<region>.amazonaws.com",
    "DefaultBucket": "my-bucket",
    "ForcePathStyle": false
  }
}
```

For MinIO use `"ServiceUrl": "http://localhost:9000"` and `"ForcePathStyle": true`.

`AccessKey` / `SecretKey` are secrets: inject them from `Granit.Vault` or
environment variables (`BlobStorage__AccessKey`, `BlobStorage__SecretKey`) —
never commit them to `appsettings.json`.

Optionally add an S3 connectivity readiness probe:

```csharp
healthChecks.AddGranitS3HealthCheck();
```

## Required companion

The S3 module provides object storage (presigned URLs, uploads) and registers the
`IBlobStorage` orchestrator (`DefaultBlobStorage`). That orchestrator, however,
depends on `IBlobDescriptorReader` / `IBlobDescriptorWriter` for descriptor
persistence, which the S3 module does **not** register. You must also register a
descriptor store — the framework's implementation is `Granit.BlobStorage.EntityFrameworkCore`
via `AddGranitBlobStorageEntityFrameworkCore(...)` (or a custom `IBlobDescriptorStore`).
Without one, `IBlobStorage` cannot be resolved (DI failure at runtime). Both the
provider and a descriptor store are required for a functional setup.

## Documentation

See the [full documentation](https://granit-fx.dev).
