namespace Granit.Workflow.Endpoints.Options;

/// <summary>
/// Configuration options for the workflow administration endpoints.
/// </summary>
public sealed class WorkflowEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Workflow:Endpoints";

    /// <summary>
    /// Route prefix for all workflow endpoints.
    /// Default: <c>"workflow"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "workflow";

    /// <summary>
    /// OpenAPI tag name for grouping workflow endpoints in Swagger UI.
    /// Default: <c>"Workflow"</c>.
    /// </summary>
    public string TagName { get; set; } = "Workflow";
}
