using Granit.Workflow.Domain;

namespace Granit.Templating.Store;

/// <summary>
/// Summary projection of a template for list views (one row per logical
/// <c>(TenantId, Name, Culture)</c> key, surfacing the most recent non-archived revision).
/// </summary>
/// <remarks>
/// Exposed to the Query Engine via <c>IQueryableSource&lt;TemplateSummary&gt;</c>.
/// </remarks>
public sealed class TemplateSummary
{
    public Guid? TenantId { get; init; }
    public required string Name { get; init; }
    public required string? Culture { get; init; }
    public required string MimeType { get; init; }
    public required WorkflowLifecycleStatus CurrentStatus { get; init; }
    public required DateTimeOffset LastModifiedAt { get; init; }
    public required string LastModifiedBy { get; init; }
    public required bool HasPublishedVersion { get; init; }
    public string? LayoutName { get; init; }
    public Guid? CategoryId { get; init; }
}
