namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>Request to create a new legal document draft.</summary>
/// <param name="DocumentId">Stable business identifier (e.g., <c>"privacy-policy"</c>).</param>
/// <param name="DisplayName">Human-readable document title.</param>
/// <param name="Description">Optional admin-only changelog note.</param>
/// <param name="TemplateName">Optional Granit.Templating template name for rendered content.</param>
public sealed record LegalDocumentCreateRequest(
    string DocumentId,
    string DisplayName,
    string? Description = null,
    string? TemplateName = null);
