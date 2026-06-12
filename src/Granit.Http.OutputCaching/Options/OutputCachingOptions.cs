namespace Granit.Http.OutputCaching.Options;

/// <summary>
/// Configuration options for Granit output caching. Section <c>"Http:OutputCaching"</c> in <c>appsettings.json</c>.
/// </summary>
public sealed class OutputCachingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Http:OutputCaching";

    /// <summary>
    /// Default response cache duration applied to the base policy.
    /// Default: 60 seconds.
    /// </summary>
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Query parameters included in cache key variation for all policies.
    /// Covers pagination, sorting, filtering, and resource expansion.
    /// Default: <c>["page", "pageSize", "sort", "filter", "q", "include", "expand"]</c>.
    /// </summary>
    public string[] VaryByQueryKeys { get; set; } =
        ["page", "pageSize", "sort", "filter", "q", "include", "expand"];

    /// <summary>
    /// Enables tenant-aware cache isolation via <c>ICurrentTenant</c>.
    /// When <c>true</c> and a tenant context is available, responses are varied by tenant ID.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableTenantIsolation { get; set; } = true;

    /// <summary>
    /// Excludes authenticated responses from caching (private-response isolation).
    /// When <c>true</c>, requests with an authenticated identity bypass output caching entirely.
    /// Default: <c>true</c>.
    /// </summary>
    public bool ExcludeAuthenticatedResponses { get; set; } = true;
}
