namespace Granit.MultiTenancy.Endpoints.Permissions;

/// <summary>
/// Permission constants for the multi-tenancy management endpoints.
/// </summary>
public static class MultiTenancyPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "MultiTenancy";

    /// <summary>Permissions for the tenants resource.</summary>
    public static class Tenants
    {
        /// <summary>Grants read access to tenant information.</summary>
        public const string Read = "MultiTenancy.Tenants.Read";

        /// <summary>Grants permission to create new tenants.</summary>
        public const string Create = "MultiTenancy.Tenants.Create";

        /// <summary>Grants permission to update tenant details.</summary>
        public const string Update = "MultiTenancy.Tenants.Update";

        /// <summary>Grants permission to activate/deactivate tenants.</summary>
        public const string Manage = "MultiTenancy.Tenants.Manage";
    }
}
