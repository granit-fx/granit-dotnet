namespace Granit.Features.Endpoints.Permissions;

/// <summary>
/// Permission constants for the feature management endpoints.
/// </summary>
public static class FeaturesPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Features";

    /// <summary>
    /// Grants read access to feature definitions.
    /// </summary>
    public const string Read = "Features.Read";

    /// <summary>
    /// Grants write access to feature overrides (set, delete).
    /// </summary>
    public const string Manage = "Features.Manage";
}
