using Granit.Workflow.Domain;

namespace Granit.Templating.Endpoints.Dtos;

public sealed record TemplateListItemResponse(
    string Name,
    string? Culture,
    string MimeType,
    WorkflowLifecycleStatus CurrentStatus,
    DateTimeOffset LastModifiedAt,
    string LastModifiedBy,
    bool HasPublishedVersion,
    string? LayoutName);
