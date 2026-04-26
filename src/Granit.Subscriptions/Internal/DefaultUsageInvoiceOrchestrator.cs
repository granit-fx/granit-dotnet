using Granit.Commands;
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
    ICommandSender commandSender,
    ILogger<DefaultUsageInvoiceOrchestrator> logger) : IUsageInvoiceOrchestrator
{
    public async Task CreateInvoiceAsync(
        CreateUsageInvoiceRequest request,
        CancellationToken cancellationToken = default)
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

        // ADR-036: ProductId on the Subscription line is taken from the resolved
        // PlanPrice (current price for the (currency, interval) slot) — survives
        // price versioning, since the PlanPrice carries its own ProductId across
        // versions. PlanPriceId-pinned subscriptions resolve through the same path.
        Guid? subscriptionProductId = plan
            .GetCurrentPrice(subscription.Currency, plan.DefaultInterval)
            ?.ProductId;

        if (basePrice > 0)
        {
            lineItems.Add(new CreateInvoiceLineItem(
                $"{plan.Name} — {plan.DefaultInterval}",
                1, basePrice,
                InvoiceSourceType.Subscription,
                subscription.Id.ToString(),
                ProductId: subscriptionProductId));
        }

        // Tier-aware total: when the resolved PlanPrice carries Tiers + a TieringMode,
        // ResolveUsageAmountAsync runs the bracket math (Volume or Graduated) and returns
        // the total directly. For flat per-unit pricing it falls back to quantity × unit
        // price — same number as the legacy code path. We still surface a derived "unit
        // price" on the invoice line so consumers can sanity-check the math; for tiered
        // pricing this is the effective average rate, not an authoritative per-unit price.
        decimal usageTotal = await pricingResolver.ResolveUsageAmountAsync(
            subscription.PlanId, subscription.Currency, plan.DefaultInterval,
            request.MeterDefinitionId.ToString(), request.AggregatedValue,
            subscription.PlanPriceId, cancellationToken)
            .ConfigureAwait(false);

        decimal effectiveUnitPrice = request.AggregatedValue > 0m
            ? usageTotal / request.AggregatedValue
            : 0m;

        lineItems.Add(new CreateInvoiceLineItem(
            $"{request.MeterName}: {request.AggregatedValue} {request.Unit}",
            request.AggregatedValue, effectiveUnitPrice,
            InvoiceSourceType.Usage,
            request.MeterDefinitionId.ToString(),
            ProductId: request.MeterProductId));

        var command = new CreateInvoiceCommand(
            TenantId: request.TenantId,
            Currency: subscription.Currency,
            CollectionMethod: CollectionMethod.Auto,
            BillingReason: BillingReason.SubscriptionCycle,
            LineItems: lineItems,
            ContactId: subscription.ContactId.Value,
            PeriodStart: request.PeriodStart,
            PeriodEnd: request.PeriodEnd);

        await commandSender.SendAsync(command, cancellationToken).ConfigureAwait(false);
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
