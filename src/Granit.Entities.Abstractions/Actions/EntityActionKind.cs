namespace Granit.Entities.Actions;

/// <summary>
/// Closed catalog of action kinds the renderer knows how to surface (per ADR-040).
/// Each kind binds to a specific frontend behaviour and a specific descriptor field
/// shape; the renderer dispatches on this enum and never on a free-form string.
/// </summary>
public enum EntityActionKind
{
    /// <summary>HTTP write call (POST / PUT / DELETE) to <c>UrlTemplate</c>; optional confirmation modal.</summary>
    ApiCall,

    /// <summary>HTTP GET against <c>UrlTemplate</c> returning a binary payload — opens the browser's download dialog.</summary>
    Download,

    /// <summary>Client-side navigation to <c>UrlTemplate</c> (route or external URL).</summary>
    Navigate,

    /// <summary>
    /// Workflow state transition resolved through the entity's
    /// <c>WorkflowDefinitionType</c>; the renderer consults the workflow runtime
    /// to know whether the transition is currently allowed for the row.
    /// <c>WorkflowTransitionName</c> carries the target state name.
    /// </summary>
    WorkflowTransition,

    /// <summary>
    /// Pure-frontend action that opens the entity's side drawer (peek). When
    /// <c>UrlTemplate</c> is <see langword="null"/>, the renderer falls back to
    /// the manifest's <c>details["default"]</c> layout for the row. When set,
    /// the renderer fetches the URL and renders the result inside the drawer
    /// (escape hatch for non-default detail surfaces).
    /// </summary>
    OpenDrawer,

    /// <summary>
    /// Pure-frontend action that opens a modal dialog. When
    /// <c>UrlTemplate</c> is <see langword="null"/>, the renderer falls back
    /// to the manifest's <c>forms["default"]</c> layout for the row (typical
    /// inline-edit case). When set, the renderer fetches the URL and renders
    /// the result inside the modal (e.g. import / export wizards).
    /// </summary>
    OpenModal,
}
