using Granit.Features;
using Granit.Features.Plans;
using Granit.Modularity;
using Granit.Subscriptions.Features.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Subscriptions.Features;

/// <summary>
/// Bridge module connecting Granit.Subscriptions to the Granit.Features cascade.
/// Registers <see cref="SubscriptionPlanIdProvider"/> and <see cref="PlanFeatureValueStore"/>.
/// </summary>
[DependsOn(
    typeof(GranitFeaturesModule),
    typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsFeaturesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IPlanIdProvider, SubscriptionPlanIdProvider>();
        context.Services.AddScoped<IPlanFeatureStore, PlanFeatureValueStore>();
    }
}
