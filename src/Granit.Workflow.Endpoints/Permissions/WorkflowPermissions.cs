namespace Granit.Workflow.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Workflow.Endpoints</c> module.
/// Use these names when granting permissions via <c>IPermissionManagerWriter.SetAsync()</c>
/// or when checking access via <c>IPermissionChecker.IsGrantedAsync()</c>.
/// </summary>
public static class WorkflowPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Workflow";

    /// <summary>Permissions for workflow history resource.</summary>
    public static class History
    {
        /// <summary>
        /// Grants read access to the workflow transition history endpoint (ISO 27001 audit trail).
        /// </summary>
        public const string Read = "Workflow.History.Read";
    }

    /// <summary>Permissions for workflow transition resource.</summary>
    public static class Transitions
    {
        /// <summary>
        /// Grants read access to query available workflow transitions for a given state.
        /// </summary>
        public const string Read = "Workflow.Transitions.Read";

        /// <summary>
        /// Grants permission to execute workflow state transitions.
        /// </summary>
        public const string Execute = "Workflow.Transitions.Execute";
    }
}
