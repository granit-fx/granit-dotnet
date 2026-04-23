namespace Granit.DataLookup.Endpoints.Permissions;

/// <summary>
/// Permission constants for Granit.DataLookup endpoints.
/// </summary>
/// <remarks>
/// Per-lookup permissions are expressed via <c>ILookupSource.RequiredPermission</c>
/// and enforced at dispatch time. The <see cref="Lookups.Read"/> permission gates the
/// manifest endpoint and is the default required policy for the route group.
/// </remarks>
public static class DataLookupPermissions
{
    /// <summary>Group name used by <c>IPermissionDefinitionProvider</c>.</summary>
    public const string GroupName = "DataLookup";

    /// <summary>Permissions for the lookup resource.</summary>
    public static class Lookups
    {
        /// <summary>Grants read access to the manifest and the lookup search endpoints.</summary>
        public const string Read = "DataLookup.Lookups.Read";
    }
}
