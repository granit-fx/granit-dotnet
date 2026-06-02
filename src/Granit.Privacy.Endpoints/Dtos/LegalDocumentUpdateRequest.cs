using Granit.Domain;

namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>Request to update a legal document draft.</summary>
/// <param name="DisplayName">Human-readable document title.</param>
/// <param name="Description">Optional admin-only changelog note.</param>
/// <param name="TemplateName">Optional Granit.Templating template name for rendered content.</param>
/// <param name="DocumentBlobId">Optional blob ID for a downloadable document (PDF, DOCX, etc.).</param>
/// <param name="ConcurrencyStamp">Stamp from the last read; must match the stored value (prevents lost updates).</param>
public sealed record LegalDocumentUpdateRequest(
    string DisplayName,
    string? Description,
    string? TemplateName,
    Guid? DocumentBlobId,
    string ConcurrencyStamp) : IConcurrencyStampRequest;
