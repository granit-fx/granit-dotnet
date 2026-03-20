namespace Granit.Templating.Endpoints.Permissions;

/// <summary>
/// Permission constants for the template administration endpoints.
/// </summary>
public static class TemplatingPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Templating";

    /// <summary>Permissions for the templates resource.</summary>
    public static class Templates
    {
        /// <summary>Grants read-only access to view templates (list, detail, history).</summary>
        public const string Read = "Templating.Templates.Read";

        /// <summary>
        /// Grants management access to template administration endpoints
        /// (save draft, delete draft, publish, unpublish).
        /// </summary>
        public const string Manage = "Templating.Templates.Manage";
    }
}
