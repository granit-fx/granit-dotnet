using Granit.Invoicing;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Internal;

/// <summary>
/// Creates consolidated invoices (fixed + usage) for PerUnit/Tiered plans.
/// Exclusive invoice creator for plans with usage components.
/// </summary>
internal sealed partial class DefaultUsageInvoiceOrchestrator(
    ISubscriptionReader subscriptionReader,
    IPlanReader planReader,
    IPricingResolver pricingResolver,
    IInvoiceCommandPublisher invoiceCommandPublisher,
    ILogger<DefaultUsageInvoiceOrchestrator> logger) : IUsageInvoiceOrchestrator
{
    public async Task CreateInvoiceAsync(
        CreateUsageInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TenantId == Guid.Empty)
        {
            Log.InvalidEto(logger, "TenantId is empty");
            return;
        }

        if (request.PeriodEnd <= request.PeriodStart)
        {
            Log.InvalidEto(logger, "PeriodEnd must be after PeriodStart");
            return;
        }

        Subscription? subscription = await subscriptionReader
            .GetActiveForTenantAsync(request.TenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            Log.NoActiveSubscription(logger, request.TenantId);
            return;
        }

        Plan? plan = await planReader
            .GetByIdAsync(subscription.PlanId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            Log.PlanNotFound(logger, subscription.PlanId);
            return;
        }

        var lineItems = new List<CreateInvoiceLineItem>();

        decimal basePrice = await pricingResolver.ResolveBasePriceAsync(
            subscription.PlanId, subscription.Currency, plan.DefaultInterval,
            subscription.PlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        if (basePrice > 0)
        {
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {plan.DefaultInterval}",
                1, basePrice,
                InvoiceSourceType.Subscription,
                subscription.Id.ToString()));
        }

        decimal unitPrice = await pricingResolver.ResolveUsageUnitPriceAsync(
            subscription.PlanId, subscription.Currency, plan.DefaultInterval,
            request.MeterDefinitionId.ToString(), subscription.PlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        lineItems.Add(new CreateInvoiceLineItem(
            $"{request.MeterName}: {request.AggregatedValue} {request.Unit}",
            request.AggregatedValue, unitPrice,
            InvoiceSourceType.Usage,
            request.MeterDefinitionId.ToString()));

        var command = new CreateInvoiceCommand(
            TenantId: request.TenantId,
            Currency: subscription.Currency,
            CollectionMethod: CollectionMethod.Auto,
            BillingReason: BillingReason.SubscriptionCycle,
            LineItems: lineItems,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd);

        await invoiceCommandPublisher.PublishAsync(command, cancellationToken).ConfigureAwait(false);
        Log.UsageInvoiceCreated(logger, request.TenantId, request.MeterName, request.AggregatedValue);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "No active subscription for tenant {TenantId}, skipping usage invoice")]
        public static partial void NoActiveSubscription(ILogger logger, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Usage invoice created for tenant {TenantId}: {MeterName} = {Value}")]
        public static partial void UsageInvoiceCreated(ILogger logger, Guid tenantId, string meterName, decimal value);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid UsageSummaryReadyEto received: {Reason}")]
        public static partial void InvalidEto(ILogger logger, string reason);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plan {PlanId} not found for usage invoice")]
        public static partial void PlanNotFound(ILogger logger, Guid planId);
    }
}
