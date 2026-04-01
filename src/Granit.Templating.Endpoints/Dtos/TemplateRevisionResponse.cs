using Granit.Templating.Store;

namespace Granit.Templating.Endpoints.Dtos;

/// <summary>
/// Response DTO for a single template revision.
/// </summary>
/// <param name="RevisionId">Unique identifier of this revision.</param>
/// <param name="Content">Raw template source content (HTML).</param>
/// <param name="MimeType">MIME type of the template content.</param>
/// <param name="Status">Lifecycle status at the time of retrieval.</param>
/// <param name="CreatedAt">UTC timestamp when this revision was created.</param>
/// <param name="CreatedBy">Identity of the user who created this revision.</param>
/// <param name="PublishedAt">UTC timestamp when this revision was published, or <c>null</c>.</param>
/// <param name="PublishedBy">Identity of the user who published this revision, or <c>null</c>.</param>
/// <param name="LayoutName">
/// Layout template name assigned to this template.
/// <c>null</c> means the code-level <c>ILayoutRegistry</c> default applies.
/// </param>
public sealed record TemplateRevisionResponse(
    Guid RevisionId,
    string Content,
    string MimeType,
    TemplateLifecycleStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    string? LayoutName);
