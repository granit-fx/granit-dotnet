using Granit.Workflow.Domain;

namespace Granit.Templating.Endpoints.Dtos;

public sealed record TemplateRevisionSummaryResponse(
    Guid RevisionId,
    WorkflowLifecycleStatus Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    int ContentLength);
