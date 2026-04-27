using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.CustomerBalance.Options;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.CustomerBalance.Internal;

/// <summary>
/// Default implementation of <see cref="ICreditExpiringScanService"/>. Queries
/// promotional credits expiring within the lead-time window (excluding those alerted
/// during the cooldown window), emits one <see cref="CreditExpiringEto"/> per match,
/// and stamps <c>BalanceTransaction.LastExpirationNotifiedAt</c> upstream so the next
/// run does not re-alert.
/// </summary>
internal sealed partial class DefaultCreditExpiringScanService(
    IBalanceTransactionReader transactionReader,
    IBalanceTransactionWriter transactionWriter,
    IBalanceAccountReader accountReader,
    IDistributedEventBus eventBus,
    IClock clock,
    IOptions<CustomerBalanceOptions> options,
    CustomerBalanceMetrics metrics,
    ILogger<DefaultCreditExpiringScanService> logger) : ICreditExpiringScanService
{
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.ScanExpiringCredits);

        DateTimeOffset now = clock.Now;
        CustomerBalanceOptions value = options.Value;
        DateTimeOffset windowEnd = now.AddDays(value.ExpirationLeadTimeDays);
        DateTimeOffset cooldownThreshold = now.AddDays(-value.ExpirationNotificationCooldownDays);

        Log.ScanStarted(logger, now, value.ExpirationLeadTimeDays, value.ExpirationNotificationCooldownDays);

        IReadOnlyList<BalanceTransaction> candidates = await transactionReader
            .GetCreditsExpiringSoonAsync(now, windowEnd, cooldownThreshold, cancellationToken)
            .ConfigureAwait(false);

        int processed = 0;

        foreach (BalanceTransaction credit in candidates)
        {
            BalanceAccount? account = await accountReader
                .GetByIdAsync(credit.BalanceAccountId, cancellationToken).ConfigureAwait(false);

            if (account is null)
            {
                continue;
            }

            await eventBus.PublishAsync(
                new CreditExpiringEto(
                    account.Id,
                    account.TenantId!.Value,
                    account.PartyId.Value,
                    credit.Id,
                    credit.Amount,
                    account.Currency,
                    credit.ExpiresAt!.Value),
                cancellationToken).ConfigureAwait(false);

            await transactionWriter
                .StampExpirationNotifiedAsync(credit.Id, now, cancellationToken)
                .ConfigureAwait(false);

            metrics.RecordExpiringNotified(account.TenantId.Value.ToString(), account.Currency);
            processed++;
        }

        Log.ScanCompleted(logger, processed);
        return processed;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Credit expiring scan started at {Timestamp} (leadTime={LeadTimeDays}d, cooldown={CooldownDays}d)")]
        public static partial void ScanStarted(ILogger logger, DateTimeOffset timestamp, int leadTimeDays, int cooldownDays);

        [LoggerMessage(Level = LogLevel.Information, Message = "Credit expiring scan completed, {Count} CreditExpiringEto emitted")]
        public static partial void ScanCompleted(ILogger logger, int count);
    }
}
