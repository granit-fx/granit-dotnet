using Granit.Commands;
using Granit.Invoicing.Commands;
using Granit.Invoicing.Domain;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Timing;
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
    ICommandSender commandSender,
    IClock clock,
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
        // Pre-flight: short-circuit on the inbound plan's pricing model. If the
        // BillingCycleCompleted event came from a usage plan, no flat invoice is
        // ever produced — skip the subscription + phase lookups entirely.
        // (Edge case: a SubscriptionPhase swapping the active plan from Flat to
        // PerUnit / Tiered would be unusual and is intentionally not honored
        // here — the upstream event carries the stored PlanId.)
        Plan? inboundPlan = await planReader.GetByIdAsync(PlanId.Create(planId), cancellationToken)
            .ConfigureAwait(false);

        if (inboundPlan is null)
        {
            Log.PlanNotFound(logger, planId);
            return;
        }

        if (inboundPlan.PricingModel is PricingModel.PerUnit or PricingModel.Tiered)
        {
            Log.SkippingUsagePlan(logger, subscriptionId, inboundPlan.PricingModel);
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

        // Phase resolution: at billing time, an active SubscriptionPhase covering "now"
        // pins the plan + optional override price + optional discount for this cycle.
        // Falls back to subscription.PlanId / planPriceId when no phase covers "now"
        // (single-plan subscriptions = backward compatible with pre-Phase-3 behavior).
        SubscriptionPhase? activePhase = subscription.GetActivePhase(clock.Now);
        PlanId effectivePlanId = activePhase?.PlanId ?? subscription.PlanId;
        Guid? effectivePlanPriceId = activePhase?.OverridePriceId ?? subscription.PlanPriceId;
        decimal? phaseDiscountPercent = activePhase?.DiscountPercent;

        // Re-resolve the plan only when the active phase points to a different one.
        Plan plan = effectivePlanId.Value == planId
            ? inboundPlan
            : (await planReader.GetByIdAsync(effectivePlanId, cancellationToken).ConfigureAwait(false))
                ?? inboundPlan;

        decimal basePrice = await pricingResolver.ResolveBasePriceAsync(
            effectivePlanId, subscription.Currency, plan.DefaultInterval,
            effectivePlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        // Apply the phase's flat percentage discount, if any (0–100; validated at entity creation).
        if (phaseDiscountPercent is { } pct && pct > 0m)
        {
            basePrice = decimal.Round(basePrice * (1m - (pct / 100m)), 4, MidpointRounding.ToEven);
        }

        if (basePrice <= 0)
        {
            Log.ZeroPrice(logger, subscriptionId);
            return;
        }

        var lineItems = new List<CreateInvoiceLineItem>();

        // ADR-036: ProductId tracks the resolved PlanPrice (current price for the
        // (currency, interval) slot). If a SubscriptionPhase pinned an OverridePriceId
        // pointing to a different PlanPrice, prefer that one — overrides may carry
        // their own ProductId for a renamed/refactored catalog item.
        Guid? subscriptionProductId =
            (effectivePlanPriceId is { } pinnedId
                ? plan.Prices.FirstOrDefault(p => p.Id == pinnedId)
                : plan.GetCurrentPrice(subscription.Currency, plan.DefaultInterval))
                ?.ProductId;

        if (plan.PricingModel == PricingModel.PerSeat)
        {
            int seatCount = subscription.Seats.Count;
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {seatCount} seat(s)",
                seatCount, basePrice,
                InvoiceSourceType.Subscription,
                subscriptionId.ToString(),
                ProductId: subscriptionProductId));
        }
        else
        {
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {plan.DefaultInterval}",
                1, basePrice,
                InvoiceSourceType.Subscription,
                subscriptionId.ToString(),
                ProductId: subscriptionProductId));
        }

        var command = new CreateInvoiceCommand(
            TenantId: tenantId,
            Currency: subscription.Currency,
            CollectionMethod: CollectionMethod.Auto,
            BillingReason: BillingReason.SubscriptionCycle,
            LineItems: lineItems,
            PartyId: subscription.PartyId.Value,
            PeriodStart: periodStart,
            PeriodEnd: periodEnd);

        await commandSender.SendAsync(command, cancellationToken).ConfigureAwait(false);
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
