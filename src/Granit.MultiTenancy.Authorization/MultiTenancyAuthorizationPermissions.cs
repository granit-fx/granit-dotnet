namespace Granit.MultiTenancy.Authorization;

/// <summary>
/// Permission constants emitted by <c>Granit.MultiTenancy.Authorization</c>.
/// </summary>
public static class MultiTenancyAuthorizationPermissions
{
    /// <summary>
    /// Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.
    /// Shares the <c>MultiTenancy</c> group with <c>Granit.MultiTenancy.Endpoints</c>
    /// (GetOrAdd semantics) — the <c>Host</c> resource sits alongside <c>Tenants</c>.
    /// </summary>
    public const string GroupName = "MultiTenancy";

    /// <summary>Permissions for the host scope.</summary>
    public static class Host
    {
        /// <summary>Grants a Host operator the ability to impersonate any tenant
        /// via <c>X-Tenant-Id</c> (or any non-JWT tenant resolver).</summary>
        public const string Impersonate = "MultiTenancy.Host.Impersonate";
    }
}
