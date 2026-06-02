using Granit.Workflow.Domain;

namespace Granit.Templating.Endpoints.Dtos;

/// <param name="RevisionId">Unique identifier of this revision.</param>
/// <param name="Content">Raw template source content.</param>
/// <param name="MimeType">MIME type of the content (e.g. <c>text/html</c>).</param>
/// <param name="Status">Lifecycle status (<c>Draft</c>, <c>Published</c>, or <c>Archived</c>).</param>
/// <param name="CreatedAt">UTC timestamp of creation.</param>
/// <param name="CreatedBy">Identity of the user who created this revision.</param>
/// <param name="PublishedAt">UTC timestamp of publication; <c>null</c> for drafts.</param>
/// <param name="PublishedBy">Identity of the user who published; <c>null</c> for drafts.</param>
/// <param name="LayoutName">Assigned layout template name; <c>null</c> uses the registry default.</param>
/// <param name="ConcurrencyStamp">Opaque optimistic-concurrency token. Pass back in update requests to detect concurrent modifications (HTTP 409).</param>
public sealed record TemplateRevisionResponse(
    Guid RevisionId,
    string Content,
    string MimeType,
    WorkflowLifecycleStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    string? LayoutName,
    string ConcurrencyStamp);
