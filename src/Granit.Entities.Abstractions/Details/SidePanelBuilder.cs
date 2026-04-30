namespace Granit.Entities.Details;

/// <summary>
/// Fluent builder for the side-panel rail of a detail view. Panels render in
/// declaration order; each panel carries an optional permission gate.
/// </summary>
public sealed class SidePanelBuilder
{
    private readonly List<SidePanelDescriptor> _panels = [];
    private int _nextOrder;

    /// <summary>Add the Audit panel (Granit.Auditing).</summary>
    public SidePanelBuilder Audit(string? requiresPermission = null) => Add(SidePanelKind.Audit, requiresPermission);

    /// <summary>Add the Timeline panel (Granit.Timeline) — past comments, attachments, system events.</summary>
    public SidePanelBuilder Timeline(string? requiresPermission = null) => Add(SidePanelKind.Timeline, requiresPermission);

    /// <summary>Add the Comments-only subset of Timeline.</summary>
    public SidePanelBuilder Comments(string? requiresPermission = null) => Add(SidePanelKind.Comments, requiresPermission);

    /// <summary>Add the Documents panel (Granit.DocumentGeneration).</summary>
    public SidePanelBuilder Documents(string? requiresPermission = null) => Add(SidePanelKind.Documents, requiresPermission);

    /// <summary>Add the Activities panel (Granit.Activities — Phase 2).</summary>
    public SidePanelBuilder Activities(string? requiresPermission = null) => Add(SidePanelKind.Activities, requiresPermission);

    private SidePanelBuilder Add(SidePanelKind kind, string? requiresPermission)
    {
        _panels.Add(new SidePanelDescriptor
        {
            Kind = kind,
            Order = _nextOrder++,
            RequiresPermission = requiresPermission,
        });
        return this;
    }

    internal IReadOnlyList<SidePanelDescriptor> Build() => _panels.AsReadOnly();
}
