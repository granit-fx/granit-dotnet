# Granit.Documents.PublicLinks.EntityFrameworkCore

EF Core persistence companion for `Granit.Documents.PublicLinks` (F18.2).
Owns the `documents_public_links` table and ships the `IDocumentPublicLinkStore`
+ `IDocumentPublicLinkService` implementations.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.PublicLinks.EntityFrameworkCore
```

## Storage model

| Concern | Mapping |
| --- | --- |
| `TokenHash` (HMAC-SHA256 digest) | `bytea` / `varbinary(32)`, fixed length 32, **unique** index |
| `Scope` | stored as string (`Download`, `View`) — operator-readable |
| `(DocumentId, RevokedAt)` index | listing query (newest first, active surfaced) |
| `(TenantId, ExpiresAt)` index | per-tenant cleanup of expired links |
| Tenant isolation | `IMultiTenant` filter inherited from `ApplyGranitConventions` — bypassed by `ResolveByTokenHashAsync` (anonymous lookup) |

The framework ships **no migrations** (per the documents convention): hosts run
`EnsureCreated` / their own `Add-Migration` against the isolated context.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsEntityFrameworkCore(opts => opts.UseNpgsql(conn));
builder.AddGranitDocumentsPublicLinks(opts =>
{
    opts.SigningKey = vaultPepper;   // mandatory — empty key rejects mint requests
});
builder.AddGranitDocumentsPublicLinksEntityFrameworkCore(opts => opts.UseNpgsql(conn));
```

## Security notes

- The raw bearer token is returned exactly once by `CreateAsync`, never persisted.
- HMAC pepper is sourced from Vault via `Granit.Configuration.Vault`. Rotating
  the pepper invalidates every existing link — by design.
- `ResolveByTokenHashAsync` bypasses the tenant filter on purpose (anonymous
  endpoint, no `ICurrentTenant`); authorisation is re-anchored on the link's
  own `TenantId` before the document is served.
