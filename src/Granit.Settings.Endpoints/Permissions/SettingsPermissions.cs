namespace Granit.Settings.Endpoints.Permissions;

/// <summary>
/// Permission constants for the settings administration endpoints.
/// </summary>
public static class SettingsPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Settings";

    /// <summary>Permissions for the global settings resource.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Global is the correct domain term for application-wide settings; VB keyword conflict is acceptable in this context.")]
    public static class Global
    {
        /// <summary>Grants read access to global settings.</summary>
        public const string Read = "Settings.Global.Read";

        /// <summary>Grants write access to global settings.</summary>
        public const string Manage = "Settings.Global.Manage";
    }

    /// <summary>Permissions for the tenant settings resource.</summary>
    public static class Tenant
    {
        /// <summary>Grants read access to tenant settings.</summary>
        public const string Read = "Settings.Tenant.Read";

        /// <summary>Grants write access to tenant settings.</summary>
        public const string Manage = "Settings.Tenant.Manage";
    }
}
