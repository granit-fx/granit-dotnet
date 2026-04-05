using Granit.Workflow.Domain;

namespace Granit.Templating.Endpoints.Dtos;

internal sealed record TemplateListQueryParameters(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    WorkflowLifecycleStatus? Status = null,
    string? Culture = null,
    Guid? CategoryId = null);
