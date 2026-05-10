# Granit.Documents.Renditions.EntityFrameworkCore

EF Core persistence for `Granit.Documents.Renditions`. Owns the
`documents_renditions` table + the `RenditionStore` implementation, and
subscribes to `DocumentPermanentlyDeletedEvent` so rendition blobs are released
alongside their parent document.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.Renditions.EntityFrameworkCore
```

Then in the host:

```csharp
builder.AddGranitDocumentsEntityFrameworkCore(opts => opts.UseNpgsql(connString));
builder.AddGranitDocumentsRenditionsEntityFrameworkCore(opts => opts.UseNpgsql(connString));
```

## What this package owns

- `RenditionsDbContext` (isolated from `DocumentsDbContext`).
- `documents_renditions` table — keyed on `(DocumentVersionId, Type, Format)`.
- `RenditionStore : IRenditionStore` — list / find / add / update / cascade delete.
- `DocumentPermanentlyDeletedRenditionHandler` — soft-deletes the rendition
  blobs and decrements `TenantStorageQuota.RenditionUsageBytes` when a parent
  document is permanently deleted.

## Quota tracking

`TenantStorageQuota` gains a `RenditionUsageBytes` column (additive — defaults
to `0`, no backfill required). The total billable footprint exposed to operators
is `UsageBytes + RenditionUsageBytes`; `LimitBytes` keeps gating user uploads
only.
