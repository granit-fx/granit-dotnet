using Granit.Workflow.Domain;

namespace Granit.Templating.Store;

/// <summary>
/// Summary projection of a template for list views.
/// </summary>
public sealed class TemplateSummary
{
    public required string Name { get; init; }
    public required string? Culture { get; init; }
    public required string MimeType { get; init; }
    public required WorkflowLifecycleStatus CurrentStatus { get; init; }
    public required DateTimeOffset LastModifiedAt { get; init; }
    public required string LastModifiedBy { get; init; }
    public required bool HasPublishedVersion { get; init; }
    public string? LayoutName { get; init; }
}
