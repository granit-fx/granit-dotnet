# Granit.Documents.AssetMetadata.EntityFrameworkCore

EF Core persistence companion for `Granit.Documents.AssetMetadata` (F17.2).
Owns the `documents_asset_metadata` table and the `AssetMetadataStore`
implementation; cascades on `DocumentPermanentlyDeletedEvent`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.AssetMetadata.EntityFrameworkCore
```

## Storage model

| Concern | Mapping |
| --- | --- |
| Typed columns (`Width`, `CameraMake`, `TakenAt`, `PageCount`, `DurationMs`, …) | direct EF Core nullable columns — SQL-queryable |
| `RawMetadata` (verbatim extractor payload) | JSON string via value converter — column type stays portable: `text` on Postgres, `nvarchar(max)` on SQL Server, `TEXT` on SQLite |
| Uniqueness | `(DocumentVersionId)` unique index → idempotent re-runs |
| Tenant isolation | `IMultiTenant` filter inherited from `ApplyGranitConventions` |

Hosts running on Postgres who want JSONB query support
(`raw_metadata @> '{"exif:Make":"Canon"}'::jsonb`) run the following in their
own migration — the framework ships no migrations:

```sql
ALTER TABLE documents_asset_metadata
  ALTER COLUMN raw_metadata TYPE jsonb USING raw_metadata::jsonb;
```

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsEntityFrameworkCore(opts => opts.UseNpgsql(conn));
builder.AddGranitDocumentsAssetMetadata();
builder.AddGranitDocumentsAssetMetadataEntityFrameworkCore(opts => opts.UseNpgsql(conn));
```

## Cascade

`DocumentPermanentlyDeletedAssetMetadataHandler` subscribes to the parent
`DocumentPermanentlyDeletedEvent` and removes every asset-metadata row attached
to the deleted document. No blob lifecycle — the source bytes are owned by
`DocumentVersion`, the metadata rows are purely derived.
