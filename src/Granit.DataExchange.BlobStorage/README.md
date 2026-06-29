# Granit.DataExchange.BlobStorage

Bridges `Granit.DataExchange` file operations to `Granit.BlobStorage`, providing a
ready-made `IDataExchangeFileProvider` backed by the registered blob storage provider
(S3, Azure Blob, FileSystem, etc.).

## Usage

Add the module to your host application:

```csharp
[DependsOn(typeof(GranitDataExchangeBlobStorageModule))]
public class MyAppModule : GranitModule { }
```

Configure the container name and default content type (both optional):

```json
{
  "DataExchange:BlobStorage": {
    "ContainerName": "data-exchange",
    "DefaultContentType": "application/octet-stream"
  }
}
```

`ContainerName` defaults to `data-exchange` and `DefaultContentType` to
`application/octet-stream`.

## How it works

This package replaces the default in-memory `IDataExchangeFileProvider` with
`BlobStorageFileProvider`, which delegates to `IBlobStoreProvider` (the internal
streaming interface implemented by every blob storage provider).

File references are S3-style object keys built by `IBlobKeyStrategy`, ensuring
multi-tenant isolation and consistent key distribution.
