namespace Granit.AI.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.AI</c>.
/// </summary>
public static class AIPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "AI";

    /// <summary>Permissions for the AI credentials resource.</summary>
    public static class Credentials
    {
        /// <summary>
        /// Grants the right to write per-tenant / per-workspace AI credentials.
        /// Required (in addition to <c>Settings.{Tenant,Global}.Manage</c>) for any
        /// <c>ISettingManager.Set*Async</c> call where the setting name starts with
        /// <c>Granit.AI.</c>. Required for the workspace credentials endpoint.
        /// </summary>
        public const string Manage = "AI.Credentials.Manage";
    }
}
