using Granit.Modularity;
using Granit.Subscriptions.Internal.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Subscriptions.Internal;

/// <summary>
/// Self-hosted subscription provider. All lifecycle management is handled by Granit locally.
/// </summary>
[DependsOn(typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsInternalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ISubscriptionProvider, InternalSubscriptionProvider>();
    }
}
