namespace Granit.Entities.Details;

/// <summary>
/// Closed enum of standard side panels a detail view may surface.
/// Each value maps to an existing Granit module — the renderer no-ops gracefully
/// when the target module isn't loaded in the host.
/// </summary>
/// <remarks>
/// Per ADR-046, this list is deliberately tight. App-specific side panels are not
/// supported in v1 of the catalog; they would be added through a future
/// <c>custom:</c> namespace extension if the need arises.
/// </remarks>
public enum SidePanelKind
{
    /// <summary>Audit log of changes to the entity (Granit.Auditing).</summary>
    Audit,

    /// <summary>Past activity feed: comments, attachments, system events (Granit.Timeline).</summary>
    Timeline,

    /// <summary>Comments thread (subset of Timeline; opt-in alternative when Timeline is too noisy).</summary>
    Comments,

    /// <summary>Generated documents attached to the entity (Granit.DocumentGeneration).</summary>
    Documents,

    /// <summary>Upcoming activities + to-dos for the entity (Granit.Activities — Phase 2).</summary>
    Activities,
}
