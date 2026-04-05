using Granit.Modularity;
using Granit.Subscriptions.Wolverine.Services;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Subscriptions.Wolverine;

/// <summary>
/// Wolverine integration for Granit.Subscriptions. Handles provider sync after
/// FSM transitions and processes inbound external events (webhooks).
/// </summary>
[DependsOn(
    typeof(GranitSubscriptionsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitSubscriptionsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<PaymentRetryDispatcher>();
    }
}
