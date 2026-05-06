namespace Granit.Documents.Endpoints.Documents.Dtos;

/// <summary>
/// Document-shaped wire-shape carrying a tag assigned to a document. Mirrors the
/// fields of <c>Granit.Taxonomy.Domain.Tag</c> exposed by the proxy endpoints —
/// keeping the response surface owned by Documents lets the Taxonomy DTOs evolve
/// without rippling breaking schema changes onto consumers of the per-document
/// tag list.
/// </summary>
public sealed record DocumentTagResponse(
    Guid Id,
    Guid? TenantId,
    string Scope,
    string Name,
    string Color,
    bool HideOnEntityCard,
    uint RowVersion);

/// <summary>List response for <see cref="DocumentTagResponse"/>.</summary>
public sealed record ListDocumentTagsResponse(IReadOnlyList<DocumentTagResponse> Items);

/// <summary>Wire-shape carrying a (document, tag) assignment row returned from the proxy assign endpoint.</summary>
public sealed record DocumentTagAssignmentResponse(
    Guid Id,
    Guid? TenantId,
    Guid TagId,
    Guid DocumentId,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId);
