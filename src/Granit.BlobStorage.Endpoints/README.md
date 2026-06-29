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

## Integration

Map the administration endpoints in `Program.cs` after the app is built:

```csharp
api.MapGranitBlobStorage();
// or customize the prefix (default: /blob-storage):
api.MapGranitBlobStorage(opts => opts.RoutePrefix = "admin/blobs");
```

This exposes upload initiation (presigned URLs), confirmation, download,
deletion (crypto-shredding), orphan cleanup, and queryable `BlobDescriptor`
endpoints. They are role-gated by the module-provided permissions
`BlobStoragePermissions.Administration.Read` (list / descriptors / query) and
`.Manage` (upload / download / delete / confirm / cleanup) — grant these in
your authorization configuration.

## Documentation

See the [full documentation](https://granit-fx.dev).
