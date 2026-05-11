# Granit.Documents.PublicLinks

Public sharing links for `Granit.Documents` (F18). HMAC-hashed tokens,
scope-bound (download / view), TTL- and use-bounded, revocable.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.PublicLinks
```

## What this package ships

- `DocumentPublicLink` aggregate — encapsulates a single shareable link
  (document pointer, HMAC-SHA256 digest of the bearer token, scope, expiry,
  use cap, revocation metadata).
- `PublicLinkToken` value object + `PublicLinkTokenFactory` — 32-byte
  URL-safe base64 tokens, deterministic HMAC hashing under the host pepper.
- `IDocumentPublicLinkService` — application surface (create / revoke / list).
  Concrete implementation lives in
  `Granit.Documents.PublicLinks.EntityFrameworkCore` (F18.2).
- Domain events (`DocumentPublicLink{Created,Revoked,Consumed}Event`) and
  integration events (`*Eto`) — the Eto payloads deliberately omit the token
  hash; credential material never leaves the database.
- `GranitDocumentsPublicLinksOptions` — `DefaultTtl` (7 d), `MaxTtl` (90 d),
  `SigningKey` (HMAC pepper, sourced from Vault), `RateLimitPerMinute`,
  `DefaultMaxUses`.
- `DocumentsPublicLinksPermissions` + provider — `Create`, `Revoke`, `Read`.
- `DocumentsPublicLinksMetrics` + `DocumentsPublicLinksActivitySource` for
  observability.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsPublicLinks();

// Storage + HTTP surface (companion packages — F18.2 / .3):
builder.AddGranitDocumentsPublicLinksEntityFrameworkCore(o => o.UseNpgsql(connString));
app.MapGroup("/api")
   .MapGranitDocuments()
   .MapGranitDocumentsPublicLinks();
```

## Security model

- Tokens are returned to the caller exactly once at creation; only the
  HMAC-SHA256 digest (under `SigningKey`) is persisted. The bus payload
  (`*Eto`) never carries the token or its hash.
- Links are time-bounded (`ExpiresAt`) and optionally use-bounded
  (`MaxUses` — `null` means unlimited within the TTL window).
- Revocation is operator-driven; redemption after revocation is rejected
  by `DocumentPublicLink.RegisterConsumption`.
- The endpoints package applies per-IP rate limiting at
  `RateLimitPerMinute` redemptions per minute.

## Observability

- Meter `Granit.Documents.PublicLinks`:
  - `granit.documents.public_links.created.count`
  - `granit.documents.public_links.revoked.count`
  - `granit.documents.public_links.consumed.count`
- `ActivitySource` `Granit.Documents.PublicLinks` with spans
  `documents.public_links.create`, `…revoke`, `…consume`.
- Tags: `tenant_id`, `scope`, `reason`.
