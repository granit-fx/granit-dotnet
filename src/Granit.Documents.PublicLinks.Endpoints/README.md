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

app.MapGroup("/api").MapGranitDocuments();
app.MapGranitDocumentsPublicLinks();
```
