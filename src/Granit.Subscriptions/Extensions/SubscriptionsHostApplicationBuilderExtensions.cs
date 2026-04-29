using Granit.Analytics.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Metering;
using Granit.QueryEngine.Extensions;
using Granit.Subscriptions.Definitions;
using Granit.Subscriptions.Diagnostics;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Exports;
using Granit.Subscriptions.Internal;
using Granit.Subscriptions.Metrics;
using Granit.Subscriptions.Queries;
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

        builder.Services.AddQueryDefinition<Subscription, SubscriptionQueryDefinition>();
        builder.Services.AddQueryDefinition<Plan, PlanQueryDefinition>();
        builder.Services.AddQueryDefinition<PlanPrice, PlanPriceQueryDefinition>();
        builder.Services.AddExportDefinition<Subscription, SubscriptionExportDefinition>();
        builder.Services.AddExportDefinition<Plan, PlanExportDefinition>();
        builder.Services.AddExportDefinition<PlanPrice, PlanPriceExportDefinition>();

        builder.Services.AddMetricDefinition<Subscription, int, ActiveSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Subscription, int, TrialSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Subscription, int, PastDueSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Subscription, int, CancelledSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Subscription, int, DunningSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Subscription, int, CancelAtPeriodEndSubscriptionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Plan, int, ActivePlanCountMetricDefinition>();
        builder.Services.AddMetricDefinition<PlanPrice, int, ActivePlanPriceCountMetricDefinition>();

        return builder;
    }
}
