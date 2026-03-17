using Granit.Core.Modularity;
using Granit.Features;
using Granit.Features.EntityFrameworkCore;
using Granit.Http.Bulkhead;
using Granit.MultiTenancy;
using Granit.RateLimiting;

namespace Granit.Bundle.SaaS;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the SaaS bundle.
/// </summary>
public static class GranitBuilderSaaSExtensions
{
    /// <summary>
    /// Adds the SaaS bundle: MultiTenancy, Features, Features.EntityFrameworkCore,
    /// RateLimiting, Bulkhead.
    /// </summary>
    public static GranitBuilder AddSaaS(this GranitBuilder builder)
    {
        builder.AddModule<GranitMultiTenancyModule>();
        builder.AddModule<GranitFeaturesModule>();
        builder.AddModule<GranitFeaturesEntityFrameworkCoreModule>();
        builder.AddModule<GranitRateLimitingModule>();
        builder.AddModule<GranitHttpBulkheadModule>();
        return builder;
    }
}
