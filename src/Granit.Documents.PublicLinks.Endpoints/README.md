# Granit.Documents.PublicLinks.Endpoints

Minimal API endpoints for `Granit.Documents.PublicLinks` (F18.3). Authenticated
admin surface for minting / revoking / listing public sharing links, plus the
anonymous token-based redemption endpoints.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Documents.PublicLinks.Endpoints
```

## Routes

### Admin (authenticated)

| Method | Path | Permission | Purpose |
| --- | --- | --- | --- |
| `POST` | `/documents/{id}/public-links` | `DocumentsPublicLinks.PublicLinks.Create` | Mint a new public link. Returns the raw token once. |
| `DELETE` | `/public-links/{id}` | `DocumentsPublicLinks.PublicLinks.Revoke` | Revoke an existing link. |
| `GET` | `/documents/{id}/public-links` | `DocumentsPublicLinks.PublicLinks.Read` | List every link for a document. |

### Anonymous

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/p/{token}` | Redirect to a presigned blob URL with `Content-Disposition: attachment`. |
| `GET` | `/p/{token}/preview` | Redirect to a presigned blob URL for inline preview. |

Every failure path on the anonymous surface returns `404 Not Found` with no body
to prevent information disclosure — invalid, expired, revoked, exhausted and
unknown tokens are indistinguishable on the wire.

## Usage

```csharp
builder.AddGranitDocuments();
builder.AddGranitDocumentsPublicLinks();
builder.AddGranitDocumentsEntityFrameworkCore(opts => opts.UseNpgsql(connString));
builder.AddGranitDocumentsPublicLinksEntityFrameworkCore(opts => opts.UseNpgsql(connString));

// F18.4 — opt in to rate limiting on the anonymous /p/{token} endpoints.
builder.Services.AddGranitDocumentsPublicLinksRateLimiter(builder.Configuration);

app.UseRateLimiter();  // required for the policy above to be enforced

app.MapGroup("/api").MapGranitDocuments();
app.MapGranitDocumentsPublicLinks();
```

## Rate limiting (F18.4)

The anonymous `/p/{token}` and `/p/{token}/preview` endpoints carry a
`RequireRateLimiting("granit-documents-public-links")` metadata. Hosts opt in by
calling `AddGranitDocumentsPublicLinksRateLimiter(configuration)` and adding
`UseRateLimiter()` to the pipeline; without those two calls the metadata is a
no-op and the endpoints are unbounded.

- **Window:** 1 minute (fixed)
- **Permit limit:** `GranitDocumentsPublicLinksOptions.RateLimitPerMinute`
  (default `60`)
- **Partition key:** SHA-256 hash of `"{client IP}|{bearer token}"`. The raw
  token never enters the limiter's partition table — the hash is computed
  in-process for defense-in-depth so a memory dump cannot recover cleartext
  bearers.
- **Rejection response:** the default ASP.NET Core `429 Too Many Requests`
  with a `Retry-After` header. No custom body — rate-limited and revoked
  tokens remain indistinguishable on the wire.

## Consumption audit trail (F18.4)

Every successful redemption (after the consumption is durably persisted)
publishes a `DocumentPublicLinkConsumedEto` integration event onto
`IDistributedEventBus`. The Eto carries:

- `LinkId`, `DocumentId`, `TenantId`, `Scope`
- `CurrentUses` (post-increment), `ConsumedAt`
- `ClientIpMasked` — anonymised to `/24` (IPv4) or `/48` (IPv6); raw IP never
  leaves the endpoint module
- `UserAgent` — verbatim `User-Agent` header (downstream consumers SHOULD
  honour retention limits)

The token is **never** included — neither in cleartext nor in hashed form.

No dedicated `PublicLinkAccessLog` table ships with the framework. Consumers
that need long-term audit storage subscribe to the Eto (audit module, SIEM
forwarder, BigQuery sink, etc.) — keeping the module decoupled from any
particular retention strategy.
