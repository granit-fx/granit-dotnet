using Granit.Modularity;
using Granit.Subscriptions.Builtin.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Subscriptions.Builtin;

/// <summary>
/// Self-hosted subscription provider. All lifecycle management is handled by Granit locally.
/// </summary>
[DependsOn(typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsBuiltinModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddSingleton<ISubscriptionProvider, BuiltinSubscriptionProvider>();
}
