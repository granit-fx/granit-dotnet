using Granit.Workflow.Domain;

namespace Granit.Templating.Endpoints.Dtos;

public sealed record TemplateRevisionResponse(
    Guid RevisionId,
    string Content,
    string MimeType,
    WorkflowLifecycleStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    string? LayoutName);
