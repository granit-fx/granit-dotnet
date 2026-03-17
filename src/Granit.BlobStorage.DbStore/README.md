# Granit.BlobStorage.DbStore

Database implementation for `Granit.BlobStorage`. Stores blob content as rows in
a relational database via EF Core. Ideal for small files and regulated environments
requiring unified backup and transactional consistency.

## How it works

Blobs are stored as `byte[]` rows in the `storage_blob_contents` table. Each row
holds the full binary content alongside its object key and tenant identifier.

Pre-signed URLs are not natively supported — use `Granit.BlobStorage.Proxy` to
provide token-based upload/download endpoints.

```text
Client ──PUT──> /api/blobs/upload/{token} ──stream──> DbStoreBlobClient.SaveAsync()
                                                        │
                                              storage_blob_contents (EF Core)
```

## Registration

```csharp
// Program.cs
builder.AddGranitBlobStorageDbStore(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BlobStorage")));
builder.AddGranitBlobStorageProxy();       // required for pre-signed URLs

var app = builder.Build();
app.MapGranitBlobProxyEndpoints();
```

## Configuration

```json
{
  "BlobStorage": {
    "MaxBlobSizeBytes": 10485760,
    "Proxy": {
      "BaseUrl": "https://api.example.com",
      "RoutePrefix": "/api/blobs"
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `MaxBlobSizeBytes` | `10485760` (10 MB) | Maximum blob size accepted by the provider |
| `UploadUrlExpiry` | `00:15:00` | TTL for proxy upload tokens |
| `DownloadUrlExpiry` | `00:05:00` | TTL for proxy download tokens |

## Multi-tenant isolation

Object key format: `{tenantId}/{containerName}/{yyyy}/{MM}/{blobId}`

All tenants share the same database table. Isolation is enforced by EF Core global
query filters via `IMultiTenant` on the `DbStoreBlobContent` entity.

## Size limits

Database storage is designed for small files (documents, certificates, configuration).
The default limit is 10 MB per blob. For larger files, use S3 or FileSystem providers.

## Security

- **Size enforcement** — blobs exceeding `MaxBlobSizeBytes` are rejected before persistence
- **UUID-only keys** — no user-supplied names in object keys
- **Multi-tenant query filters** — automatic tenant isolation via `ApplyGranitConventions`

## Dependencies

- `Granit.BlobStorage` (core abstractions)
- `Granit.Persistence` (EF Core interceptors, `ApplyGranitConventions`)
- `Granit.BlobStorage.Proxy` (for pre-signed URL endpoints — registered separately)

## License

Apache-2.0
