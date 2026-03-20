namespace Granit.Features.Endpoints.Permissions;

/// <summary>
/// Permission constants for the feature management endpoints.
/// </summary>
public static class FeaturesPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Features";

    /// <summary>Permissions for the feature flags resource.</summary>
    public static class Flags
    {
        /// <summary>Grants read access to feature definitions.</summary>
        public const string Read = "Features.Flags.Read";

        /// <summary>Grants write access to feature overrides (set, delete).</summary>
        public const string Manage = "Features.Flags.Manage";
    }
}
