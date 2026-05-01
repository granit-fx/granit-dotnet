namespace Granit.Entities.Actions;

/// <summary>
/// Closed catalog of action kinds the renderer knows how to surface (per ADR-040).
/// Each kind binds to a specific frontend behaviour and a specific descriptor field
/// shape; the renderer dispatches on this enum and never on a free-form string.
/// </summary>
public enum EntityActionKind
{
    /// <summary>HTTP write call (POST / PUT / DELETE) to <c>UrlTemplate</c>; optional confirmation modal.</summary>
    ApiCall = 0,

    /// <summary>HTTP GET against <c>UrlTemplate</c> returning a binary payload — opens the browser's download dialog.</summary>
    Download = 1,

    /// <summary>Client-side navigation to <c>UrlTemplate</c> (route or external URL).</summary>
    Navigate = 2,

    /// <summary>
    /// Workflow state transition resolved through the entity's
    /// <c>WorkflowDefinitionType</c>; the renderer consults the workflow runtime
    /// to know whether the transition is currently allowed for the row.
    /// <c>WorkflowTransitionName</c> carries the target state name.
    /// </summary>
    WorkflowTransition = 3,
}
