using Granit.Analytics.Extensions;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Invoicing;
using Granit.Payments.Diagnostics;
using Granit.Payments.Domain;
using Granit.Payments.Exports;
using Granit.Payments.Internal;
using Granit.Payments.Metrics;
using Granit.Payments.Queries;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Payments.Extensions;

/// <summary>Extension methods for registering the Granit payments infrastructure.</summary>
public static class PaymentsHostApplicationBuilderExtensions
{
    /// <summary>Adds the Granit payments infrastructure.</summary>
    public static IHostApplicationBuilder AddGranitPayments(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<PaymentsMetrics>();
        builder.Services.TryAddTransient<IAutoChargeService, DefaultAutoChargeService>();
        builder.Services.TryAddScoped<IInvoicePrePaymentProcessor, PassThroughPrePaymentProcessor>();
        builder.Services.TryAddTransient<IWebhookProcessor, DefaultWebhookProcessor>();
        GranitActivitySourceRegistry.Register(PaymentsActivitySource.Name);

        builder.Services.TryAddScoped<IPaymentProviderResolver, DefaultPaymentProviderResolver>();
        builder.Services.TryAddSingleton<IPaymentMethodAvailabilityFilter, DefaultPaymentMethodAvailabilityFilter>();

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<PaymentTransaction, PaymentTransactionQueryDefinition>();
        builder.Services.AddQueryDefinition<PaymentMethod, PaymentMethodQueryDefinition>();
        builder.Services.AddQueryDefinition<Refund, RefundQueryDefinition>();
        builder.Services.AddQueryDefinition<Dispute, DisputeQueryDefinition>();
        builder.Services.AddExportDefinition<PaymentTransaction, PaymentTransactionExportDefinition>();
        builder.Services.AddExportDefinition<PaymentMethod, PaymentMethodExportDefinition>();
        builder.Services.AddExportDefinition<Refund, RefundExportDefinition>();
        builder.Services.AddExportDefinition<Dispute, DisputeExportDefinition>();

        builder.Services.AddMetricDefinition<PaymentTransaction, int, SuccessfulPaymentTransactionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<PaymentTransaction, decimal, SuccessfulPaymentTransactionTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<PaymentTransaction, int, FailedPaymentTransactionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<PaymentTransaction, int, PendingPaymentTransactionCountMetricDefinition>();
        builder.Services.AddMetricDefinition<PaymentMethod, int, DefaultPaymentMethodCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Refund, int, SuccessfulRefundCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Refund, decimal, SuccessfulRefundTotalMetricDefinition>();
        builder.Services.AddMetricDefinition<Dispute, int, OpenDisputeCountMetricDefinition>();
        builder.Services.AddMetricDefinition<Dispute, decimal, OpenDisputeTotalMetricDefinition>();

        return builder;
    }
}
