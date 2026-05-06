namespace Granit.MultiTenancy;

/// <summary>
/// HTTP feature placed on <c>HttpContext.Features</c> by
/// <c>RequireHostContextEndpointFilter</c> when an endpoint marked
/// <c>.AllowHostAccess()</c> is invoked in host mode.
/// </summary>
/// <remarks>
/// Consumed by the HTTP-backed <see cref="IHostAccessContext"/> implementation.
/// Lives on the request feature collection (not in DI) so the signal is naturally
/// scoped to the request and never leaks across requests. Use
/// <see cref="HostMode"/> to obtain the canonical instance to store.
/// </remarks>
public interface IHostAccessFeature
{
    /// <summary>Always <c>true</c> on instances stored on the feature collection.</summary>
    bool IsHostAccess { get; }

    /// <summary>Canonical singleton stored on <c>HttpContext.Features</c>.</summary>
    public static IHostAccessFeature HostMode { get; } = new HostModeFeature();

    private sealed class HostModeFeature : IHostAccessFeature
    {
        public bool IsHostAccess => true;
    }
}
