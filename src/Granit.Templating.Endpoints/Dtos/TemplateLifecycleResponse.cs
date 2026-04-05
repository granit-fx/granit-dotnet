using Granit.Workflow.Domain;

namespace Granit.Templating.Endpoints.Dtos;

public sealed record TemplateLifecycleResponse(
    string Name,
    string? Culture,
    WorkflowLifecycleStatus CurrentStatus,
    bool WorkflowEnabled,
    IReadOnlyList<WorkflowLifecycleStatus> AvailableTransitions);
