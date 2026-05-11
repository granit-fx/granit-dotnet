using Granit.Documents.PublicLinks.Domain;

namespace Granit.Documents.PublicLinks.Endpoints.Dtos;

/// <summary>
/// HTTP shape of a single <see cref="DocumentPublicLink"/> aggregate as exposed by
/// the listing endpoint. The raw token and its HMAC digest are deliberately omitted
/// — the bearer string is shown to the operator exactly once at creation time.
/// </summary>
public sealed record PublicLinkResponse(
    Guid Id,
    Guid DocumentId,
    PublicLinkScope Scope,
    DateTimeOffset ExpiresAt,
    int? MaxUses,
    int CurrentUses,
    DateTimeOffset? RevokedAt,
    string? RevocationReason,
    DateTimeOffset CreatedAt);
