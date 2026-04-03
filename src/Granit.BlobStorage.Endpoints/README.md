# Granit.BlobStorage.Endpoints

Minimal API endpoints for administering Granit blob storage. Upload initiation (presigned URLs), upload confirmation (validation pipeline), download URL generation, deletion (crypto-shredding), orphan cleanup, and queryable descriptors. Protected by configurable role-based authorization.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BlobStorage.Endpoints
```

## Dependencies

- `Granit.BlobStorage`
- `Granit.Authorization`
- `Granit.QueryEngine.AspNetCore`
- `Granit.Validation`

## Documentation

See the [full documentation](https://granit-fx.dev).
