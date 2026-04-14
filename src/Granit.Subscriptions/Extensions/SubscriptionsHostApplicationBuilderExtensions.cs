using Granit.Diagnostics;
using Granit.Metering;
using Granit.Subscriptions.Definitions;
using Granit.Subscriptions.Diagnostics;
using Granit.Subscriptions.Internal;
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
        builder.Services.TryAddTransient<IPeriodAdvancementService, DefaultPeriodAdvancementService>();
        builder.Services.TryAddTransient<ICancelAtPeriodEndService, DefaultCancelAtPeriodEndService>();
        builder.Services.TryAddTransient<ISubscriptionReactivationService, DefaultSubscriptionReactivationService>();
        builder.Services.TryAddTransient<IDunningService, DefaultDunningService>();
        builder.Services.TryAddTransient<ISubscriptionProviderSyncService, DefaultSubscriptionProviderSyncService>();
        builder.Services.TryAddTransient<IUsageInvoiceOrchestrator, DefaultUsageInvoiceOrchestrator>();
        builder.Services.TryAddTransient<IBillingCycleInvoiceOrchestrator, DefaultBillingCycleInvoiceOrchestrator>();
        // Override the calendar-month default: quota must track the subscription billing period.
        builder.Services.AddScoped<IBillingPeriodProvider, SubscriptionBillingPeriodProvider>();
        GranitActivitySourceRegistry.Register(SubscriptionsActivitySource.Name);

        return builder;
    }
}
