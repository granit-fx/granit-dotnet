using Granit.Caching;
using Granit.Features.Diagnostics;
using Granit.Features.Extensions;
using Granit.Features.Internal;
using Granit.Localization;
using Granit.Localization.Options;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Features;

/// <summary>
/// Granit module for SaaS feature management.
/// </summary>
/// <remarks>
/// Provides plan-based feature activation with multi-level resolution:
/// Tenant override → Plan value → Default (code).
/// <para>
/// Values are cached via <c>HybridCache</c> (L1 in-process, L2 Redis if configured)
/// with per-tenant invalidation via <see cref="Events.FeatureValueChangedEvent"/>.
/// </para>
/// <para>
/// The application must implement <see cref="Plans.IPlanIdProvider"/> and
/// <see cref="Plans.IPlanFeatureStore"/> to activate plan-level resolution.
/// Without these, features fall back to their default values.
/// </para>
/// <para>
/// Multi-tenancy support is optional. If <c>GranitMultiTenancyModule</c> is registered,
/// tenant-level feature overrides are resolved automatically. Without it, the cascade
/// falls through Plan → Default with no DI error.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitCachingModule))]
[DependsOn(typeof(GranitLocalizationModule))]
public sealed partial class GranitFeaturesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitFeatures();

        context.Services.Configure<GranitLocalizationOptions>(options =>
        {
            options.Resources
                .Add<FeaturesLocalizationResource>("fr")
                .AddJson(
                    typeof(FeaturesLocalizationResource).Assembly,
                    "Granit.Features.Localization.Features");
        });
    }

    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        IHostEnvironment environment = context.ServiceProvider
            .GetRequiredService<IHostEnvironment>();

        if (!environment.IsDevelopment() &&
            context.ServiceProvider.GetRequiredService<IFeatureStoreReader>() is InMemoryFeatureStore)
        {
            ILogger<GranitFeaturesModule> logger = context.ServiceProvider
                .GetRequiredService<ILogger<GranitFeaturesModule>>();

            FeaturesLog.InMemoryStoreActiveInNonDevelopment(logger);
        }
    }
}
