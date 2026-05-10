---
title: "ADR-052: Granit.Documents — user-managed file & asset module"
description: "Granit introduces a new Granit.Documents module for user-managed files organised in folders, with owners, ACL sharing, mandatory versioning, tenant-level quota, and tags. The module composes on top of Granit.BlobStorage (which keeps its role as the binary store) and absorbs DAM concerns (renditions, asset metadata) as opt-in extensions in later phases."
sidebar:
  order: 52
  label: "052 - Documents module"
---

> **Date:** 2026-05-02
> **Authors:** Jean-Francois Meyers
> **Scope:** NEW modules `Granit.Documents`, `Granit.Documents.EntityFrameworkCore`, `Granit.Documents.Endpoints`, `Granit.Documents.BackgroundJobs`, `Granit.Documents.Notifications` (phase 1). Future opt-in extensions: `Granit.Documents.Renditions`, `Granit.Documents.AssetMetadata`, `Granit.Documents.PublicLinks`, `Granit.Documents.Indexing`, `Granit.Documents.Workflow`, `Granit.Documents.Collections`.
> **Status:** Accepted (tagging story superseded by [ADR-054](054-taxonomy-module.md) — Documents wires up to `Granit.Taxonomy` instead of building its own `DocumentTag` join.)

## Context

Granit currently exposes file storage through `Granit.BlobStorage` — a low-level, content-addressable binary store with provider implementations for S3, Azure Blob, GCS, and FileSystem. `BlobStorage` handles upload presigning, MIME validation, soft-delete with GDPR retention, and tenant isolation, but it deliberately knows nothing about higher-level concepts: there is no folder hierarchy, no per-file owner, no sharing ACL, no version history, no tenant quota, no tags. Two Granit modules consume `BlobStorage` directly today — `Granit.DataExchange.BlobStorage` (transient import/export artifacts) and `Granit.Privacy.BlobStorage` (GDPR export archives) — both of which are *system-generated* binaries, not user-curated documents.

Hosted apps that need a real document management experience (the kind users expect from Odoo Documents, SharePoint, or Google Drive) currently have to roll their own folder/share/version layer on top of `BlobStorage`. Each app reinvents:

- A folder tree with parent/child relationships and tenant isolation.
- A `Document` entity carrying owner, tags, descriptions, and a pointer to a blob.
- An ACL or share-link mechanism layered on permissions.
- Versioning when a file is replaced — usually missing or broken.
- Tenant-level storage quota enforcement.

Beyond pure document management, several apps also have Digital Asset Management (DAM) requirements: derivative renditions (thumbnails, web-sized, print-sized), metadata extraction (EXIF / IPTC / XMP for images, dimensions for video), curated cross-folder collections, and brand approval workflows. These needs overlap with document management at roughly 70 % — the core (folders, owner, share, tags, versions, quota) is identical; only the asset-specific pipelines differ.

A third category surfaced during design: **entity-attribute files** — the logo on a `Party`, an avatar on a `User`, the main image on a future `Product`. These are 1:1 with a domain entity, accessed via that entity, and have no documentary identity of their own. Odoo confirms the pattern: `res.partner.image_*` is stored on the partner record itself, not in the Documents app.

This ADR locks the shape of the new module, the boundary with `BlobStorage`, the phasing strategy, and the storage decision matrix that future modules will reference.

## Decision drivers

- **Modular framework, not an ERP.** Apps that only need binary storage must keep using `BlobStorage` directly without taking on the documentary stack.
- **No breaking changes to existing consumers.** `Granit.DataExchange.BlobStorage` and `Granit.Privacy.BlobStorage` continue unchanged; `Documents` arrives alongside, not as a replacement.
- **DAM and document management share a core.** A single module with phased extensions is cheaper than two parallel modules duplicating folders, share, quota, and search.
- **Pre-1.0 freedom.** No migration constraints; clean breaking changes if needed.
- **Solo-maintainer cost.** Phase 1 must be shippable in a bounded number of stories; advanced DAM concerns (renditions, AI search, collaboration) defer to later phases.
- **Compliance-grade by default.** ISO 27001 audit trail and GDPR-compatible erasure are baseline expectations, not opt-ins.

## Considered alternatives

### A — Adapt `Granit.BlobStorage` in place

Add folder, owner, share, version, tag, quota tables directly inside `Granit.BlobStorage`.

- **Pros**: single module, no new package surface.
- **Cons**: pollutes a clean low-level primitive with high-level domain concepts. Forces every existing consumer (`DataExchange`, `Privacy`) to depend on documentary tables they do not need. Breaks the layered architecture — `BlobStorage` would no longer be reusable as a pure binary store.
- **Rejected**.

### B — Two parallel modules: `Granit.Documents` + `Granit.AssetLibrary`

`Documents` handles bureaucratic files (contracts, forms); `AssetLibrary` handles DAM (images, videos, brand assets).

- **Pros**: clean conceptual separation between "documents" and "assets".
- **Cons**: 70 % of features (folders, owner, share, tags, versions, quota, search) duplicated across both. UX confusion at the application layer ("where do I store this signed PDF that has a watermark?"). Double maintenance cost forever.
- **Rejected**.

### C — Single `Granit.Documents` with opt-in DAM extensions (CHOSEN)

One module covering both document management and DAM concerns. The core (folders, owner, share, tags, versions, quota) is universal. DAM-specific pipelines (renditions, metadata extraction, public links, indexing) ship as opt-in extension packages activated only when the host needs them.

- **Pros**: one mental model, one DbContext, one set of admin endpoints. Apps that need only documents pull the core. Apps that need DAM add the relevant extensions. Cost amortised across both audiences.
- **Cons**: broad surface to design upfront; mitigated by a strict phasing plan (see [Phasing](#phasing)).
- **Chosen**.

## Decision

### Module layout

```text
Granit.Documents (NEW, phase 1)
  ├── Domain
  │     ├── Folder         (aggregate root, multi-tenant, tree)
  │     ├── Document       (aggregate root, multi-tenant, owner UserId, primary blob)
  │     ├── DocumentVersion (entity under Document, blob FK)
  │     ├── DocumentShare  (entity, ACL grant)
  │     └── Tag            (entity, tenant-scoped vocabulary)
  ├── Quotas               (tenant-level storage quota service)
  ├── Queries              (DocumentQueryDefinition, FolderQueryDefinition)
  ├── Exports              (DocumentExportDefinition)
  ├── Metrics              (TotalStorageUsedMetricDefinition, DocumentCountMetricDefinition)
  └── Diagnostics          (DocumentsMetrics, DocumentsActivitySource)

Granit.Documents.EntityFrameworkCore (NEW, phase 1)
  └── isolated DbContext + migrations + entity configs

Granit.Documents.Endpoints (NEW, phase 1)
  ├── Folders     (CRUD, move, list, breadcrumb)
  ├── Documents   (upload via BlobStorage presigned → finalize, download, rename, move, delete)
  ├── Versions    (list, download, restore, delete-old)
  ├── Shares      (grant, revoke, list)
  ├── Tags        (CRUD, autocomplete)
  ├── Search      (name + tags filters, paging)
  └── Quotas      (read-only — admin sees tenant usage)

Granit.Documents.BackgroundJobs (NEW, phase 1)
  ├── OrphanDocumentCleanupJob   (documents pointing at missing blobs)
  ├── EmptyTrashJob              (soft-deleted documents past retention window)
  └── QuotaRecomputeJob          (drift correction on tenant.usage_bytes)

Granit.Documents.Notifications (NEW, phase 1)
  ├── DocumentSharedNotificationType
  ├── DocumentShareRevokedNotificationType
  ├── QuotaWarningNotificationType         (80 % of tenant quota)
  └── QuotaExceededNotificationType        (block on upload)

— phase 2 extensions (separate ADRs / stories) —
Granit.Documents.Renditions       (depends on Granit.Imaging)
Granit.Documents.AssetMetadata    (EXIF / IPTC / XMP / ID3 extraction)
Granit.Documents.PublicLinks      (token-based, expiring, anonymous access)
Granit.Documents.Indexing         (content full-text — PDF, Office)
Granit.Documents.Workflow         (per-folder opt-in workflow on versions)

— phase 3 extensions —
Granit.Documents.Collections      (curated cross-folder sets, lightboxes)
Granit.Documents.Automation       (Odoo-style rules: auto-tag, auto-move)
```

### Domain model (phase 1)

```text
Folder (AggregateRoot, IMultiTenant)
  ├── Id, TenantId
  ├── ParentFolderId  (NULL ONLY for the tenant root — invariant enforced)
  ├── Name, Path (materialised, "/" for root, "/A/B/C" otherwise), Depth
  ├── OwnerUserId
  ├── IsTenantRoot (bool — exactly one row per tenant has this true)
  ├── Status (Active, Trashed)
  └── CreatedAt, UpdatedAt, audit fields
  Invariants:
    - IsTenantRoot folders cannot be moved, renamed, trashed, or deleted
    - Path is recomputed on rename/move (own + descendants in transaction)

Document (AggregateRoot, IMultiTenant)
  ├── Id, TenantId
  ├── FolderId             (NOT NULL — every document lives in a folder, root included)
  ├── OwnerUserId
  ├── Name, Description, Tags
  ├── CurrentVersionId (FK to active DocumentVersion)
  ├── Status (Active, Trashed, PermanentlyDeleted)
  ├── RowVersion (concurrency token)
  ├── CreatedAt, UpdatedAt, TrashedAt?, audit fields
  └── Versions (1..N, owned)

DocumentVersion (Entity, owned by Document)
  ├── Id, DocumentId
  ├── VersionNumber (monotonic per document)
  ├── BlobDescriptorId (FK to Granit.BlobStorage)
  ├── SizeBytes, ContentType, ContentHash
  ├── UploadedByUserId, UploadedAt
  └── CommitMessage? (free-text changelog)

DocumentShare (Entity)
  ├── Id, TenantId
  ├── TargetType (Folder | Document)        ─┐
  ├── FolderId    (when TargetType = Folder) │ exactly one is non-null
  ├── DocumentId  (when TargetType = Document)┘
  ├── GranteeType (User | Role | Group)
  ├── GranteeId
  ├── Permission (Read | Edit | Manage)
  ├── IsDefault (bool — folder shares only; inherits to descendants automatically via path query)
  ├── ExpiresAt? (nullable)
  └── CreatedAt, CreatedByUserId

Tag (Entity, tenant-scoped vocabulary)
  ├── Id, TenantId, Name (unique per tenant)
  └── Color? (UI hint)

DocumentTag (join, Document × Tag)
```

Database invariants (DDL-level, enforced regardless of application code):

```sql
-- Exactly one root per tenant
CREATE UNIQUE INDEX ux_folder_one_root_per_tenant
  ON folders (tenant_id) WHERE is_tenant_root = TRUE;

-- ParentFolderId NULL ⇔ IsTenantRoot
ALTER TABLE folders
  ADD CONSTRAINT ck_folder_parent_only_root_null
  CHECK (parent_folder_id IS NOT NULL OR is_tenant_root = TRUE);

-- DocumentShare TargetType / FK consistency
ALTER TABLE document_shares
  ADD CONSTRAINT ck_share_target_exactly_one
  CHECK (
    (target_type = 'Folder'   AND folder_id   IS NOT NULL AND document_id IS NULL)
    OR
    (target_type = 'Document' AND document_id IS NOT NULL AND folder_id   IS NULL)
  );
```

### Storage decision matrix (canonical reference)

This matrix governs every future module that needs to persist binary content. Cite this section in story descriptions to settle "should this go in `BlobStorage` or `Documents`?" debates once and for all.

| Category | Layer | Examples | Why |
| -------- | ----- | -------- | --- |
| **System-transient binaries** | `Granit.BlobStorage` direct | GDPR export archive, CSV import, generated invoice PDF (pre-archival), audit dump, cache snapshot | No folder concept, no user owner, often time-bounded retention, no UX surface |
| **Entity-attribute files** | `Granit.BlobStorage` direct, FK on the owning aggregate | `Party.AvatarBlobId` (logo / photo), future `Product.MainImageBlobId`, `User` profile picture | 1:1 with a domain aggregate, accessed via that aggregate, no documentary identity. Matches Odoo's `res.partner.image_*` pattern. |
| **Standalone documents and assets** | `Granit.Documents`, FK at `Document.Id` if referenced from elsewhere | Contracts, marketing files, brand assets, attachments uploaded to a folder, anything that lives in a documentary tree | First-class identity, owner, sharing, versioning, audit trail, potential renditions and metadata |

Self-test for future module authors: *"Does the user navigate to a folder to find this file?"* — yes → `Documents`. *"Is this file conceptually an attribute of a single record?"* — yes → `BlobStorage` attribute. *"Is this file generated by the system with no user UX?"* — `BlobStorage` transient.

### Composition with existing modules

- **`Granit.BlobStorage`** — composed. Every `DocumentVersion` references one `BlobDescriptor.Id`. Each phase-2 rendition is a separate `BlobDescriptor`. `Documents` knows about `BlobStorage`; `BlobStorage` does not know about `Documents`.
- **`Granit.MultiTenancy`** — soft dependency (per the framework convention). All entities are `IMultiTenant`; quota is enforced per `TenantId`.
- **`Granit.Authorization`** — `DocumentsPermissions` provider with `Documents.Documents.Read|Manage`, `Documents.Folders.Read|Manage`, `Documents.Shares.Manage`, `Documents.Quotas.Read`. ACL on `DocumentShare` complements coarse permission gates.
- **`Granit.Auditing`** — every Document and DocumentVersion mutation is auditable (ISO 27001 A.12.4.1 — "who downloaded what when").
- **`Granit.Notifications`** — share invitations, quota warnings, expiration alerts (phase 2 for public links).
- **`Granit.RateLimiting`** — applied to upload / download endpoints; orthogonal to storage quota (which is on `TenantId.UsageBytes`).
- **`Granit.Imaging`** — phase 2 dependency for `Granit.Documents.Renditions`. Phase 1 ignores it.
- **`Granit.Workflow`** — phase 2 dependency for `Granit.Documents.Workflow`, an opt-in extension that allows a folder to attach a `WorkflowDefinition` driving each new `DocumentVersion` through state transitions (`Draft → Reviewed → Approved → Archived`). Without that extension, versioning is mechanical (every upload increments the number, latest is current).
- **`Granit.QueryEngine` + `Granit.DataExchange`** — `Document` and `Folder` ship with `QueryDefinition` and `ExportDefinition` (admin grid + CSV/XLSX export, per ADR-020).
- **`Granit.Analytics`** — `MetricDefinition` for `TotalStorageUsed` (per tenant) and `DocumentCount` (per tenant, per folder), pairing with the queries per the D1 archi rule.

### Tenant root folder bootstrap

Every tenant has exactly one **invisible root folder** (`Folder.IsTenantRoot = true`,
`ParentFolderId = NULL`, `Path = "/"`). All user-created folders descend from it; all
documents reference a folder (root included). The root unifies SQL: there is no
`FolderId IS NULL` branch to handle, and tenant-wide permission grants become
"shares on the root folder".

**Bootstrap strategy: lazy and idempotent.**

```csharp
// Called at the start of every Documents write endpoint
public interface IDocumentBootstrapService
{
    Task<Guid> EnsureTenantRootAsync(CancellationToken ct);
}

// Implementation (sketch):
//   1. SELECT id FROM folders WHERE tenant_id = @t AND is_tenant_root = TRUE
//   2. if found → return id (cache result in IMemoryCache for the request scope)
//   3. else → INSERT … ON CONFLICT DO NOTHING; re-SELECT and return
// The unique conditional index guarantees idempotency under concurrent first-access.
```

**Why lazy over eager (Wolverine handler on `TenantCreatedEto`)**:

- Zero coupling to `Granit.MultiTenancy`'s lifecycle events — works for tenants that
  pre-existed the Documents module being installed.
- No async lag between tenant creation and root availability.
- Race on first concurrent access is absorbed by the unique index + `ON CONFLICT DO
  NOTHING` (PostgreSQL) / catch unique-constraint-violation (SQLite).
- Cost is one SELECT per tenant per process startup (cached thereafter).

UI considerations:

- `GET /api/v1/documents/folders` returns the children of the tenant root by default,
  not the root itself. Client never has to know about the root.
- Breadcrumb starts at "My documents" (localised label), not at "/".
- Renaming, moving, trashing, or deleting the root is rejected at the aggregate
  level (`ThrowIfTenantRoot()` guard on `Folder.Rename`, `MoveTo`, `Trash`).

### Permission resolution model

ACL resolution must be **single-query, non-recursive, indexed** to keep list and
search endpoints fast. Granit relies on **materialised path** (`Folder.Path`) plus
**FusionCache** for the hot read path. No share-row materialisation per descendant
(write amplification on every grant + folder move was rejected).

#### Effective ACL query

For a request "can principal P read document D?":

```sql
-- Resolves to a single non-recursive query
SELECT s.permission, s.expires_at
FROM document_shares s
WHERE s.tenant_id = @tenant_id
  AND s.grantee_id IN (@user_id_and_roles_and_groups)
  AND (s.expires_at IS NULL OR s.expires_at > NOW())
  AND (
    -- Direct share on the document
    (s.target_type = 'Document' AND s.document_id = @document_id)
    OR
    -- Share on the document's folder or any ancestor folder (path prefix)
    (s.target_type = 'Folder' AND s.folder_id IN (
      SELECT f.id
      FROM folders f
      WHERE f.tenant_id = @tenant_id
        AND (
          f.id = @document_folder_id                           -- direct folder
          OR @document_folder_path LIKE f.path || '/%'         -- ancestor (prefix scan)
        )
    ))
  );
```

Effective permission = highest of (Read, Edit, Manage) returned by the query. No
deny-override in phase 1 (purely additive grants).

#### Required indexes

```sql
-- Path prefix scan (PostgreSQL btree with text_pattern_ops handles LIKE 'prefix%' efficiently)
CREATE INDEX ix_folder_tenant_path ON folders (tenant_id, path text_pattern_ops);

-- Share lookup paths
CREATE INDEX ix_share_grantee_folder ON document_shares (tenant_id, grantee_id, folder_id)
  WHERE target_type = 'Folder';
CREATE INDEX ix_share_grantee_document ON document_shares (tenant_id, grantee_id, document_id)
  WHERE target_type = 'Document';
```

#### Cache layer

`FusionCache` (already in the framework) absorbs the hot path:

- **Cache key**: `acl:{tenantId}:doc:{documentId}:user:{userId}` — hash of the user's
  effective grant set (User ID + sorted role IDs + sorted group IDs).
- **TTL**: 5 minutes default (configurable). Granted permissions are not security-
  critical to revoke instantly; 5 min lag is acceptable for non-emergency revocations.
- **Tag invalidation** (FusionCache tags):
  - `acl:{tenantId}:doc:{documentId}` — invalidated on any share change directly on
    the document.
  - `acl:{tenantId}:folder:{folderId}` — invalidated on share change on the folder.
    On any folder structural change (rename / move), all descendants' acl tags are
    invalidated by emitting one `FolderPathChanged` event whose handler walks the
    descendant set (via path prefix) and emits per-folder invalidation.
- **Target**: ≥ 95 % cache hit ratio in steady state, p99 miss path < 50 ms.

#### Why not Option A (per-descendant share materialisation)

Rejected after explicit comparison:

- Grant on a top-level folder with N descendants → O(N) row inserts. A grant on
  the tenant root would touch every document in the tenant.
- Folder move → O(N) re-materialisation of inherited shares for every descendant.
- Concurrency: documents uploaded mid-materialisation may miss the inherited grant.
- Storage: K shares × N descendants per folder, redundant data.

Path + cache pays the materialisation cost **once on the path column** (which is
already maintained for breadcrumb), not per share row.

#### Future: deny-override

If phase 2 or 3 introduces deny-override semantics ("grant on parent, explicit
deny on child"), the resolution model is revisited — likely a separate
`DocumentDeny` table evaluated alongside the additive grant query, not a switch
to materialisation.

### Versioning model (phase 1)

Versioning is **mandatory and autonomous** — no dependency on `Granit.Workflow` in phase 1.

- Every upload on an existing `Document` creates a new `DocumentVersion` with a monotonic `VersionNumber`.
- The latest version is `Document.CurrentVersionId`; older versions remain queryable and downloadable.
- An admin (with `Documents.Documents.Manage`) can restore a previous version — sets `CurrentVersionId` to the chosen older version, increments to a new version row pointing at the same blob (linear history, no branches).
- A retention job (`DocumentVersionPruningJob`) is **out of phase 1 scope**; tenants keep all versions until manually deleted. Phase 2 will introduce a per-folder retention policy.

The phase-2 `Granit.Documents.Workflow` extension layers state machines on top: a folder may attach a `WorkflowDefinition`; new versions enter the workflow's initial state and only become `Document.CurrentVersionId` once the workflow reaches a "published" state. Without the extension, versions are immediately current.

### Quota model (phase 1)

- One row per tenant in `TenantStorageQuota` (Id, TenantId, LimitBytes, UsageBytes, UpdatedAt).
- `UsageBytes` updated synchronously on each upload finalize and trash-empty operation; reconciled by `QuotaRecomputeJob` daily.
- Upload is rejected with `403 Forbidden` (RFC 7807 Problem) if `UsageBytes + incomingSize > LimitBytes`.
- `LimitBytes` is provisioned by the host application (out of framework scope to set the value — Granit ships a sensible default; SaaS hosts override per subscription tier).
- Per-user and per-folder quotas are explicitly **out of phase 1**.

### Search (phase 1)

- Full-text on `Document.Name` and `Folder.Name` via PostgreSQL `tsvector` columns generated by EF Core 10 conventions.
- Tag filters via the join table.
- Owner filter, date-range filter, content-type filter via `QueryDefinition`.
- **Content indexing** (PDF body, Office documents) is phase 2 — `Granit.Documents.Indexing` extension.

## Phasing

Tracked as a single Epic with three phases. Only phase 1 is actively planned; phase 2 and phase 3 are listed in the Epic for visibility but split into stories only when picked up.

### Phase 1 — Core document management (this iteration)

| Feature | Stories (rough) |
| ------- | --------------- |
| Module scaffolding (`Granit.Documents` + EF + Endpoints + module class) | 1 |
| Folder hierarchy (CRUD, move, breadcrumb, path materialisation) | 2 |
| Document aggregate + upload flow (presigned via BlobStorage, finalize, descriptor wiring) | 2 |
| Versioning (autonomous, monotonic, restore) | 1 |
| Tags (wired via [Granit.Taxonomy](054-taxonomy-module.md) — see ADR-054 / T6.1) | 1 |
| Share ACL (User / Role / Group, with inheritance from folder default) | 2 |
| Tenant quota (model, enforcement, recompute job) | 1 |
| Trash / soft-delete with retention | 1 |
| QueryDefinition / ExportDefinition / MetricDefinition (admin grid + KPI parity) | 1 |
| BackgroundJobs (orphan cleanup, empty-trash, quota recompute) | 1 |
| Notifications (share invites, quota warnings) | 1 |
| Documentation (docs-site reference module page) | 1 |

Estimated 14–16 stories.

### Phase 2 — DAM extensions and richer features

- `Granit.Documents.Renditions` (depends on `Granit.Imaging`) — thumbnail / web / print derivatives, generated on upload via background jobs.
- `Granit.Documents.AssetMetadata` — EXIF / IPTC / XMP for images, ID3 for audio, dimensions / duration for video.
- `Granit.Documents.PublicLinks` — token-based anonymous access with expiration, revocation, audit trail.
- `Granit.Documents.Indexing` — content full-text (PDF, Office) via Tika or equivalent.
- `Granit.Documents.Workflow` (depends on `Granit.Workflow`) — per-folder opt-in workflow on versions.
- Per-user and per-folder quotas.
- Version retention policies.

### Phase 3 — Advanced DAM and collaboration

- `Granit.Documents.Collections` — curated cross-folder sets (lightboxes, campaign asset bundles).
- `Granit.Documents.Automation` — Odoo-style rules ("when a file matches X, auto-tag / auto-move / trigger workflow").
- AI-powered search (visual similarity, embedding-based semantic search).
- Brand approval workflows (multi-stage, asset-specific).
- Real-time collaboration / co-editing integrations (OnlyOffice, Collabora) — out of scope.

## Migration impact on existing consumers

| Module | Today | After this ADR |
| ------ | ----- | --------------- |
| `Granit.DataExchange.BlobStorage` | Uses `IBlobStorage` for transient import/export files | **No change.** System-transient category. |
| `Granit.Privacy.BlobStorage` | Uses `IBlobStorage` for GDPR export archives | **No change.** System-transient category. |
| `Granit.Parties` (`Party.AvatarBlobId`) | Soft reference to `BlobDescriptor` for logo / photo | **No change.** Entity-attribute category — matches Odoo's `res.partner.image_*` pattern. |
| `Granit.DocumentGeneration` | Produces `DocumentResult` (in-memory binary) | **No change.** Apps that want to archive a generated PDF in a Documents folder explicitly call `IDocumentService.Import(blobId, folderId, …)`; the framework does not couple the two modules. |

No data migration required. No existing public API changes.

## Open questions

These are tracked in the Epic and resolved during implementation; they do not block this ADR.

1. ~~Loose documents at tenant root — `Document.FolderId` nullable or mandatory?~~
   **Resolved:** `Document.FolderId` is **NOT NULL**. Every tenant has an invisible
   root folder (`Folder.IsTenantRoot = true`), auto-created lazily on first write,
   and serves as the default parent. See [Tenant root folder bootstrap](#tenant-root-folder-bootstrap).
2. **Permissions on `Documents` vs. `Documents.Folders`** — split or merged? Phase-1 design splits to keep folder admin separate from per-document share management.
3. **Trash retention default** — 30 days (Odoo / Google Drive) or configurable per tenant? Lean toward 30 days default, configurable via `DocumentsOptions`.
4. **Cross-tenant share** — explicitly out of scope (multi-tenancy isolation is a hard rule). A user must hold an identity in the target tenant to receive a share.
5. **Concurrent upload to the same document** — last-write-wins on `CurrentVersionId` with optimistic concurrency on `Document.RowVersion`; no merge / conflict UI in phase 1.

## Cross-references

- [Granit.BlobStorage reference](/dotnet/data/blob-storage/) — composed primitive.
- ADR-017 — DDD aggregate / value-object strategy. `Document` and `Folder` follow the aggregate-root rules (private setters, `Create()` factory, `AddDomainEvent()`).
- ADR-020 — Declarative definitions placement. `DocumentQueryDefinition`, `DocumentExportDefinition`, `MetricDefinition` live in the base `Granit.Documents` module.
- ADR-040 — Three-tier metadata. `Document` and `Folder` ship with their own `EntityDefinition` (Tier A) for OData and admin UI exposure.
- ADR-051 — User aggregate. `Document.OwnerUserId` references `Granit.Identity.User.Id` (not `LocalIdentity` / `FederatedIdentity`).
