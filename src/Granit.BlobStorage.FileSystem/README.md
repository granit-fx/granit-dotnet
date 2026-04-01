# Granit.BlobStorage.FileSystem

Local file system implementation for `Granit.BlobStorage`. Stores blobs as
files on disk with tenant-prefix isolation. Ideal for development and simple
on-premise deployments.

## How it works

Blobs are stored as regular files under a configurable base path. The directory
structure mirrors the object key: `{BasePath}/{tenantId}/{container}/{yyyy}/{MM}/{blobId}`.

Pre-signed URLs are not natively supported — use `Granit.BlobStorage.Proxy` to
provide token-based upload/download endpoints.

```text
Client ──PUT──▶ /api/blobs/upload/{token} ──stream──▶ FileSystemBlobClient.SaveAsync()
                                                        │
                                              {BasePath}/{tenantId}/.../{blobId}
```

## Registration

```csharp
// Program.cs
builder.AddGranitBlobStorageFileSystem();
builder.AddGranitBlobStorageProxy();       // required for pre-signed URLs

var app = builder.Build();
app.MapGranitBlobProxy();
```

## Configuration

```json
{
  "BlobStorage": {
    "BasePath": "./blobs",
    "Proxy": {
      "BaseUrl": "https://api.example.com",
      "RoutePrefix": "/api/blobs"
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `BasePath` | *(required)* | Root directory for blob storage |
| `UploadUrlExpiry` | `00:15:00` | TTL for proxy upload tokens |
| `DownloadUrlExpiry` | `00:05:00` | TTL for proxy download tokens |

## Multi-tenant isolation

File path format: `{BasePath}/{tenantId}/{containerName}/{yyyy}/{MM}/{blobId}`

All tenants share the same base directory. Isolation is enforced by the tenant
prefix in the file path.

## Security

- **Path traversal protection** — object keys containing `..` are rejected
- **UUID-only filenames** — no user-supplied names in file paths
- **Directory auto-creation** — intermediate directories created on write

## Dependencies

- `Granit.BlobStorage` (core abstractions)
- `Granit.BlobStorage.Proxy` (for pre-signed URL endpoints — registered separately)

## License

Apache-2.0
