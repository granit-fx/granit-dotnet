using Granit.Templating.Keys;
using Granit.Workflow.Domain;

namespace Granit.Templating.Store;

/// <summary>
/// Filter and pagination parameters for listing templates in the admin store.
/// </summary>
public sealed record TemplateListFilter(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    WorkflowLifecycleStatus? Status = null,
    string? Culture = null,
    Guid? CategoryId = null);
