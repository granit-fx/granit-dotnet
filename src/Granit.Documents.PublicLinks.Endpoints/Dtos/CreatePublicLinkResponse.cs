using Granit.Documents.PublicLinks.Domain;

namespace Granit.Documents.PublicLinks.Endpoints.Dtos;

/// <summary>
/// Output shape for <c>POST /documents/{id}/public-links</c>. The
/// <see cref="Token"/> field is the raw bearer — returned exactly once and never
/// retrievable afterwards. <see cref="Url"/> is the fully-qualified redemption URL
/// the caller can hand off to a third party.
/// </summary>
public sealed record CreatePublicLinkResponse(
    Guid Id,
    Guid DocumentId,
    string Token,
    string Url,
    PublicLinkScope Scope,
    DateTimeOffset ExpiresAt,
    int? MaxUses);
