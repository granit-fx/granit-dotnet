# Granit.Documents

User-managed file and asset management module on top of `Granit.BlobStorage`. Folder hierarchy with
materialised path, owners, share ACL with path-based resolution and FusionCache, autonomous
versioning, tenant storage quota, and tags. DAM concerns (renditions, asset metadata, public links,
content indexing, workflows) ship as opt-in extension packages in later phases.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents
```

## Dependencies

- `Granit`
- `Granit.BlobStorage`

## Documentation

See [ADR-052](../../docs-site/src/content/docs/dotnet/architecture/adr/052-documents-module.md) for
the architecture decisions (storage decision matrix, path-based ACL resolution, tenant root folder
bootstrap, phasing strategy) and the [full documentation](https://granit-fx.dev).
