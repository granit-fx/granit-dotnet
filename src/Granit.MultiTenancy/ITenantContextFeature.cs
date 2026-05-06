using Microsoft.AspNetCore.Http.Features;

namespace Granit.MultiTenancy;

/// <summary>
/// HTTP feature that carries the active tenant for the current request.
/// Stored on <see cref="IFeatureCollection"/> by <see cref="CurrentTenant"/>
/// instead of the legacy <see cref="System.Threading.AsyncLocal{T}"/> backend.
/// </summary>
/// <remarks>
/// SECURITY: by binding the tenant value to the request's feature collection,
/// the lifetime is the request itself — there is no execution-context state
/// to leak across requests on thread-pool reuse.
/// </remarks>
public interface ITenantContextFeature
{
    /// <summary>The active tenant for the current request, or <see langword="null"/>.</summary>
    TenantInfo? Tenant { get; }
}
