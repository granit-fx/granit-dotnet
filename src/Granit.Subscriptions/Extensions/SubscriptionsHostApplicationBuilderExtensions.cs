using Granit.Diagnostics;
using Granit.Subscriptions.Definitions;
using Granit.Subscriptions.Diagnostics;
using Granit.Workflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Subscriptions.Extensions;

/// <summary>
/// Extension methods for registering the Granit subscriptions infrastructure.
/// </summary>
public static class SubscriptionsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit subscriptions infrastructure (provider-agnostic).
    /// </summary>
    /// <remarks>
    /// Registers workflow definitions and core services. Add a provider package
    /// (<c>Granit.Subscriptions.Internal</c> or <c>Granit.Subscriptions.Stripe</c>)
    /// for <see cref="ISubscriptionProvider"/> implementation.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitSubscriptions(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddWorkflow(SubscriptionWorkflows.WithTrial);
        builder.Services.AddWorkflow(SubscriptionWorkflows.Direct);
        builder.Services.TryAddSingleton<SubscriptionsMetrics>();
        GranitActivitySourceRegistry.Register(SubscriptionsActivitySource.Name);

        return builder;
    }
}
