using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.MultiTenancy;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Events;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Creates invoices for Flat/PerSeat plans when a billing cycle completes.
/// PerUnit/Tiered plans are handled exclusively by <see cref="UsageSummaryReadyHandler"/>
/// to prevent double invoicing (split-brain).
/// </summary>
internal static partial class BillingCycleCompletedHandler
{
    public static async Task HandleAsync(
        BillingCycleCompletedEto eto,
        ISubscriptionReader subscriptionReader,
        IPlanReader planReader,
        IPricingResolver pricingResolver,
        IMessageBus messageBus,
        ICurrentTenant currentTenant,
        ILogger<BillingCycleCompletedEto> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            Plan? plan = await planReader.GetByIdAsync(eto.PlanId, cancellationToken)
                .ConfigureAwait(false);

            if (plan is null)
            {
                Log.PlanNotFound(logger, eto.PlanId);
                return;
            }

            // Exclusive routing: skip if plan has a usage component — UsageSummaryReadyHandler handles those
            if (plan.PricingModel is PricingModel.PerUnit or PricingModel.Tiered)
            {
                Log.SkippingUsagePlan(logger, eto.SubscriptionId, plan.PricingModel);
                return;
            }

            Subscription? subscription = await subscriptionReader
                .GetByIdAsync(eto.SubscriptionId, cancellationToken)
                .ConfigureAwait(false);

            if (subscription is null)
            {
                Log.SubscriptionNotFound(logger, eto.SubscriptionId);
                return;
            }

            decimal basePrice = await pricingResolver.ResolveBasePriceAsync(
                subscription.PlanId, subscription.Currency, plan.DefaultInterval, cancellationToken)
                .ConfigureAwait(false);

            if (basePrice <= 0)
            {
                Log.ZeroPrice(logger, eto.SubscriptionId);
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
                    eto.SubscriptionId.ToString()));
            }
            else
            {
                lineItems.Add(new CreateInvoiceLineItem(
                    $"{plan.Name} — {plan.DefaultInterval}",
                    1, basePrice,
                    InvoiceSourceType.Subscription,
                    eto.SubscriptionId.ToString()));
            }

            var command = new CreateInvoiceCommand(
                TenantId: eto.TenantId,
                Currency: subscription.Currency,
                CollectionMethod: CollectionMethod.Auto,
                BillingReason: BillingReason.SubscriptionCycle,
                LineItems: lineItems,
                PeriodStart: eto.PeriodStart,
                PeriodEnd: eto.PeriodEnd);

            await messageBus.PublishAsync(command).ConfigureAwait(false);
            Log.InvoiceCreated(logger, eto.SubscriptionId, basePrice, subscription.Currency);
        }
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
