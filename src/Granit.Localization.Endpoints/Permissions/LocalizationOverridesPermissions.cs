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

    /// <summary>Grants read-only access to view translation overrides.</summary>
    public const string Read = "Localization.Overrides.Read";

    /// <summary>
    /// Grants management access to localization override endpoints
    /// (set override, remove override).
    /// </summary>
    public const string Manage = "Localization.Overrides.Manage";
}
