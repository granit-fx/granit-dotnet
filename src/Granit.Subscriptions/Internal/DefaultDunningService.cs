using Granit.Scheduling;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.Scheduling;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Internal;

internal sealed partial class DefaultDunningService(
    ISubscriptionReader subscriptionReader,
    ISubscriptionWriter subscriptionWriter,
    IScheduler scheduler,
    IClock clock,
    ILogger<DefaultDunningService> logger) : IDunningService
{
    private const int MaxRetries = 3;

    public async Task HandlePaymentFailureAsync(
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string currency,
        string methodType,
        string providerName,
        CancellationToken cancellationToken = default)
    {
        Subscription? subscription = await subscriptionReader
            .GetActiveForTenantAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            Log.NoSubscription(logger, tenantId);
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
                new RetryPaymentPayload(invoiceId, tenantId, amount, currency, methodType, providerName, subscription.DunningAttempt),
                retryAt,
                correlationId: $"subscription:{subscription.Id}",
                cancellationToken).ConfigureAwait(false);

            Log.RetryScheduled(logger, subscription.Id, subscription.DunningAttempt, retryAt);
        }

        await subscriptionWriter.UpdateAsync(subscription, cancellationToken).ConfigureAwait(false);
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
