namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Distinguishes the two OData mounts the module exposes. Tenant-feed
/// inherits the framework's per-tenant filter; host-feed bypasses it
/// per-query for cross-tenant analytics by SuperAdmins, behind explicit
/// strict-config gates.
/// </summary>
internal enum ODataFeedKind
{
    /// <summary>Standard tenant-scoped feed (default). Tenant filter applies; cross-tenant data is invisible.</summary>
    Tenant,

    /// <summary>Cross-tenant feed for host operators. Tenant filter is bypassed per-query via a host-supplied lambda; permissions must resolve to <c>MultiTenancySides.Host</c>; <c>IMultiTenant</c> entities require an explicit <c>AcknowledgeCrossTenantExposure</c> call.</summary>
    Host,
}
