using Granit.Invoicing;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Subscriptions.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Internal;

/// <summary>
/// Creates invoices for Flat/PerSeat plans when a billing cycle completes.
/// PerUnit/Tiered plans are handled exclusively by <see cref="DefaultUsageInvoiceOrchestrator"/>.
/// </summary>
internal sealed partial class DefaultBillingCycleInvoiceOrchestrator(
    ISubscriptionReader subscriptionReader,
    IPlanReader planReader,
    IPricingResolver pricingResolver,
    IInvoiceCommandPublisher invoiceCommandPublisher,
    ILogger<DefaultBillingCycleInvoiceOrchestrator> logger) : IBillingCycleInvoiceOrchestrator
{
    public async Task CreateInvoiceAsync(
        Guid subscriptionId,
        Guid tenantId,
        Guid planId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        Plan? plan = await planReader.GetByIdAsync(planId, cancellationToken)
            .ConfigureAwait(false);

        if (plan is null)
        {
            Log.PlanNotFound(logger, planId);
            return;
        }

        if (plan.PricingModel is PricingModel.PerUnit or PricingModel.Tiered)
        {
            Log.SkippingUsagePlan(logger, subscriptionId, plan.PricingModel);
            return;
        }

        Subscription? subscription = await subscriptionReader
            .GetByIdAsync(subscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            Log.SubscriptionNotFound(logger, subscriptionId);
            return;
        }

        decimal basePrice = await pricingResolver.ResolveBasePriceAsync(
            subscription.PlanId, subscription.Currency, plan.DefaultInterval,
            subscription.PlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        if (basePrice <= 0)
        {
            Log.ZeroPrice(logger, subscriptionId);
            return;
        }

        var lineItems = new List<CreateInvoiceLineItem>();

        if (plan.PricingModel == PricingModel.PerSeat)
        {
            int seatCount = subscription.Seats.Count;
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {seatCount} seat(s)",
                seatCount, basePrice,
                InvoiceSourceType.Subscription,
                subscriptionId.ToString()));
        }
        else
        {
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {plan.DefaultInterval}",
                1, basePrice,
                InvoiceSourceType.Subscription,
                subscriptionId.ToString()));
        }

        var command = new CreateInvoiceCommand(
            TenantId: tenantId,
            Currency: subscription.Currency,
            CollectionMethod: CollectionMethod.Auto,
            BillingReason: BillingReason.SubscriptionCycle,
            LineItems: lineItems,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd);

        await invoiceCommandPublisher.PublishAsync(command, cancellationToken).ConfigureAwait(false);
        Log.InvoiceCreated(logger, subscriptionId, basePrice, subscription.Currency);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Billing cycle invoice created for subscription {SubscriptionId}: {Amount} {Currency}")]
        public static partial void InvoiceCreated(ILogger logger, Guid subscriptionId, decimal amount, string currency);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping billing cycle for usage-based plan on subscription {SubscriptionId} (model: {PricingModel})")]
        public static partial void SkippingUsagePlan(ILogger logger, Guid subscriptionId, PricingModel pricingModel);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Plan {PlanId} not found for billing cycle")]
        public static partial void PlanNotFound(ILogger logger, Guid planId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Subscription {SubscriptionId} not found for billing cycle")]
        public static partial void SubscriptionNotFound(ILogger logger, Guid subscriptionId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Zero base price for subscription {SubscriptionId}, skipping invoice")]
        public static partial void ZeroPrice(ILogger logger, Guid subscriptionId);
    }
}
