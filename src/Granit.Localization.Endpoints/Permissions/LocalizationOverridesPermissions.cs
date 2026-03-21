namespace Granit.Localization.Endpoints.Permissions;

/// <summary>
/// Permission constants for the localization override management endpoints.
/// Use these names when granting permissions via <c>IPermissionManagerWriter.SetAsync()</c>
/// or when checking access via <c>IPermissionChecker.IsGrantedAsync()</c>.
/// </summary>
public static class LocalizationOverridesPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Localization";

    /// <summary>Permissions for the translation overrides resource.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Overrides is the correct domain term for this resource; VB keyword conflict is acceptable in this context.")]
    public static class Overrides
    {
        /// <summary>Grants read-only access to view translation overrides.</summary>
        public const string Read = "Localization.Overrides.Read";

        /// <summary>
        /// Grants management access to localization override endpoints
        /// (set override, remove override).
        /// </summary>
        public const string Manage = "Localization.Overrides.Manage";
    }
}
