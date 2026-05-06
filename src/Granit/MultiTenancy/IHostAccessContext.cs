namespace Granit.MultiTenancy;

/// <summary>
/// Reports whether the current execution context is running under a deliberate
/// host-access endpoint (route marked <c>.AllowHostAccess()</c>) as opposed to
/// an accidental tenant-context loss.
/// </summary>
/// <remarks>
/// <para>
/// Both states leave <see cref="ICurrentTenant.IsAvailable"/> at <c>false</c>, but
/// the data layer needs to distinguish them: a signaled host-access call is the
/// expected cross-tenant read path; an unsignaled call is an anomaly that may
/// indicate a tenant-context leak in flight and should raise an alert.
/// </para>
/// <para>
/// Implementations are scoped to the request and are read by
/// <c>EfStoreBase</c> when emitting the
/// <c>granit.persistence.cross_tenant_query</c> metric and the companion log
/// event. Default DI registration is the HTTP-backed implementation; callers
/// outside an HTTP request observe <see cref="IsHostAccess"/> as <c>false</c>.
/// </para>
/// </remarks>
public interface IHostAccessContext
{
    /// <summary>
    /// <c>true</c> when the current request was admitted through a route marked
    /// <c>.AllowHostAccess()</c> in host mode (no tenant header, authenticated
    /// caller). <c>false</c> otherwise.
    /// </summary>
    bool IsHostAccess { get; }
}
