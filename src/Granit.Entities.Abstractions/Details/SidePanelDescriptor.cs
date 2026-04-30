namespace Granit.Entities.Details;

/// <summary>
/// Immutable descriptor for one side panel slot on a detail view.
/// </summary>
public sealed record SidePanelDescriptor
{
    /// <summary>The standard side-panel kind (per ADR-046).</summary>
    public required SidePanelKind Kind { get; init; }

    /// <summary>Display order within the side-panel rail (lower first).</summary>
    public int Order { get; init; }

    /// <summary>
    /// Optional permission gate — drops the panel from the manifest payload when the
    /// user lacks this permission. Defense-in-depth per ADR-040 / story #1549.
    /// </summary>
    public string? RequiresPermission { get; init; }
}
