# Granit.BlobStorage.EntityFrameworkCore

EF Core persistence layer for Granit.BlobStorage. Provides BlobStorageDbContext and EfBlobDescriptorStore.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.BlobStorage.EntityFrameworkCore
```

## Dependencies

- `Granit.BlobStorage`
- `Granit.Persistence`

## Required companion provider

This package only provides descriptor persistence (`BlobStorageDbContext` +
`EfBlobDescriptorStore`, exposing `IBlobDescriptorStore` / `IBlobDescriptorReader`
/ `IBlobDescriptorWriter`). It does **not** register the `IBlobStorage`
orchestrator. You must also reference a concrete provider package —
`Granit.BlobStorage.S3`, `.AzureBlob`, `.GoogleCloud`, `.FileSystem`,
`.Database`, or `.Proxy` — and call its registration method (e.g.
`AddGranitBlobStorageS3()`). The provider package is what registers `IBlobStorage`
(`DefaultBlobStorage`); without it, injecting `IBlobStorage` fails at service
resolution.

## Configuration

Register the EF Core persistence in your infrastructure (or host) module, **after**
a blob provider:

```csharp
context.Builder.AddGranitBlobStorageS3();
context.Builder.AddGranitBlobStorageEntityFrameworkCore(
    configureShared: options => options.UseNpgsql(connectionString));
```

This registers `BlobStorageDbContext`, the `IBlobDescriptorStore` / `Reader` /
`Writer`, and the queryable source. For `SchemaPerTenant` multi-tenancy also pass
`configureSchemaPerTenant` (and for database-per-tenant, `configureDatabasePerTenant`),
otherwise the isolated DbContext factory throws at first resolve.

The bundled `BlobStorageDbContext` already calls `ConfigureBlobStorageModule()`
internally. Only if you fold blob storage into your own host/tenant DbContext do
you need to call `modelBuilder.ConfigureBlobStorageModule()` in `OnGranitModelCreating`
to emit the `BlobDescriptor` table into your migration.

## Documentation

See the [full documentation](https://granit-fx.dev).
