namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>Detailed view of a legal document version.</summary>
public sealed record LegalDocumentDetailResponse(
    Guid Id,
    string DocumentId,
    int Version,
    string LifecycleStatus,
    string DisplayName,
    string? Description,
    string? TemplateName,
    Guid? DocumentBlobId,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastModifiedAt);
