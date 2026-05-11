using Granit.Documents.PublicLinks.Domain;

namespace Granit.Documents.PublicLinks.Endpoints.Dtos;

/// <summary>Mapping helpers between <see cref="DocumentPublicLink"/> and HTTP DTOs.</summary>
internal static class PublicLinkMappingExtensions
{
    internal static PublicLinkResponse ToResponse(this DocumentPublicLink link) =>
        new(
            Id: link.Id,
            DocumentId: link.DocumentId,
            Scope: link.Scope,
            ExpiresAt: link.ExpiresAt,
            MaxUses: link.MaxUses,
            CurrentUses: link.CurrentUses,
            RevokedAt: link.RevokedAt,
            RevocationReason: link.RevocationReason,
            CreatedAt: link.CreatedAt);
}
