using Granit.MultiTenancy;
using Granit.Payments.Events;
using Granit.Scheduling;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Scheduling;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Dunning entry point. Transitions subscription to PastDue on payment failure,
/// schedules retry attempts with exponential backoff, and suspends after exhausting retries.
/// </summary>
internal static partial class PaymentFailedHandler
{
    private const int MaxRetries = 3;

    public static async Task HandleAsync(
        PaymentFailedEto eto,
        ISubscriptionReader subscriptionReader,
        ISubscriptionWriter subscriptionWriter,
        IScheduler scheduler,
        IClock clock,
        ICurrentTenant currentTenant,
        ILogger<PaymentFailedEto> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            Subscription? subscription = await subscriptionReader
                .GetActiveForTenantAsync(eto.TenantId, cancellationToken)
                .ConfigureAwait(false);

            if (subscription is null)
            {
                Log.NoSubscription(logger, eto.TenantId);
                return;
            }

            subscription.MarkPastDue();
            subscription.IncrementDunningAttempt();

            if (subscription.DunningAttempt > MaxRetries)
            {
                subscription.Suspend();
                Log.Suspended(logger, subscription.Id, subscription.DunningAttempt);
            }
            else
            {
                DateTimeOffset retryAt = CalculateRetryDate(clock.Now, subscription.DunningAttempt);
                await scheduler.ScheduleAsync(
                    new RetryPaymentPayload(eto.InvoiceId, eto.TenantId, eto.Amount, eto.Currency, eto.MethodType, eto.ProviderName, subscription.DunningAttempt),
                    retryAt,
                    correlationId: $"subscription:{subscription.Id}",
                    cancellationToken).ConfigureAwait(false);

                Log.RetryScheduled(logger, subscription.Id, subscription.DunningAttempt, retryAt);
            }

            await subscriptionWriter.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static DateTimeOffset CalculateRetryDate(DateTimeOffset now, int attempt) =>
        attempt switch
        {
            1 => now.AddDays(3),
            2 => now.AddDays(7),
            _ => now.AddDays(14),
        };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "No active subscription for tenant {TenantId} on payment failure")]
        public static partial void NoSubscription(ILogger logger, Guid tenantId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Dunning retry {Attempt} scheduled for subscription {SubscriptionId} at {RetryAt}")]
        public static partial void RetryScheduled(ILogger logger, Guid subscriptionId, int attempt, DateTimeOffset retryAt);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Subscription {SubscriptionId} suspended after {Attempts} failed payment attempts")]
        public static partial void Suspended(ILogger logger, Guid subscriptionId, int attempts);
    }
}
