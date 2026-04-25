using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.CustomerBalance.Internal;

/// <summary>Default implementation of <see cref="IPreExpirationScanService"/>.</summary>
internal sealed partial class DefaultPreExpirationScanService(
    IBalanceTransactionReader transactionReader,
    IBalanceAccountReader accountReader,
    IBalanceAccountWriter accountWriter,
    IDistributedEventBus eventBus,
    IClock clock,
    IOptions<CustomerBalanceOptions> options,
    CustomerBalanceMetrics metrics,
    ILogger<DefaultPreExpirationScanService> logger) : IPreExpirationScanService
{
    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.Now;
        int warningDays = Math.Max(1, options.Value.PreExpirationWarningDays);
        var window = TimeSpan.FromDays(warningDays);

        Log.ScanStarted(logger, now, warningDays);

        IReadOnlyList<BalanceTransaction> credits = await transactionReader
            .GetCreditsNearExpirationAsync(now, window, cancellationToken)
            .ConfigureAwait(false);

        int published = 0;

        foreach (BalanceTransaction credit in credits)
        {
            // Defensive: the reader's predicate already excludes credits with
            // ExpiresAt <= now (those belong to the expiration job), but recompute
            // here so the per-credit DaysUntilExpiration tag is consistent and so
            // any race between the two jobs cannot publish a "near-expiration"
            // signal for an already-expired credit.
            if (credit.ExpiresAt is not { } expiresAt || expiresAt <= now)
            {
                continue;
            }

            BalanceAccount? account = await accountReader
                .GetByIdAsync(credit.BalanceAccountId, cancellationToken)
                .ConfigureAwait(false);

            if (account is null)
            {
                continue;
            }

            int daysUntil = (int)Math.Floor((expiresAt - now).TotalDays);

            await eventBus.PublishAsync(
                new CreditNearExpirationEto(
                    BalanceAccountId: account.Id,
                    TenantId: account.TenantId!.Value,
                    CreditTransactionId: credit.Id,
                    ExpiringAmount: credit.Amount,
                    Currency: account.Currency,
                    ExpiresAt: expiresAt,
                    DaysUntilExpiration: daysUntil),
                cancellationToken).ConfigureAwait(false);

            credit.MarkPreExpirationNoticed(now);
            await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

            metrics.RecordPreExpirationWarning(account.TenantId.Value.ToString(), account.Currency);
            published++;
        }

        Log.ScanCompleted(logger, published);
        return published;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Pre-expiration scan started at {Timestamp} (window: {WarningDays} day(s))")]
        public static partial void ScanStarted(ILogger logger, DateTimeOffset timestamp, int warningDays);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Pre-expiration scan completed, {Count} warning(s) published")]
        public static partial void ScanCompleted(ILogger logger, int count);
    }
}
