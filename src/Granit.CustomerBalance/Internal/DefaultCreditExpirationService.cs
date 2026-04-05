using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.CustomerBalance.Events;
using Granit.Events;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.CustomerBalance.Internal;

/// <summary>
/// Default implementation of <see cref="ICreditExpirationService"/>.
/// Queries expired promotional credits, debits remaining balances,
/// and publishes <see cref="CreditExpiredEto"/>.
/// </summary>
internal sealed partial class DefaultCreditExpirationService(
    IBalanceTransactionReader transactionReader,
    IBalanceAccountReader accountReader,
    IBalanceAccountWriter accountWriter,
    IDistributedEventBus eventBus,
    IGuidGenerator guidGenerator,
    IClock clock,
    CustomerBalanceMetrics metrics,
    ILogger<DefaultCreditExpirationService> logger) : ICreditExpirationService
{
    public async Task<int> ExpireCreditsAsync(CancellationToken cancellationToken = default)
    {
        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.ExpireCredit);

        DateTimeOffset now = clock.Now;
        Log.ScanStarted(logger, now);

        IReadOnlyList<BalanceTransaction> expiredCredits = await transactionReader
            .GetExpiredCreditsAsync(now, cancellationToken).ConfigureAwait(false);

        int processed = 0;

        foreach (BalanceTransaction credit in expiredCredits)
        {
            BalanceAccount? account = await accountReader
                .GetByIdAsync(credit.BalanceAccountId, cancellationToken).ConfigureAwait(false);

            if (account is null || account.Balance <= 0)
            {
                continue;
            }

            bool alreadyExpired = account.Transactions.Any(t =>
                t.Source == TransactionSource.Expiration &&
                t.ReferenceId == credit.Id);

            if (alreadyExpired)
            {
                continue;
            }

            decimal amountToExpire = Math.Min(credit.Amount, account.Balance);

            account.Debit(
                amountToExpire,
                TransactionSource.Expiration,
                "Promotional credit expired",
                now,
                guidGenerator.Create(),
                referenceId: credit.Id,
                referenceType: "PromotionalCredit");

            await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

            await eventBus.PublishAsync(
                new CreditExpiredEto(account.Id, account.TenantId!.Value, amountToExpire, account.Currency),
                cancellationToken).ConfigureAwait(false);

            metrics.RecordExpired(account.TenantId.Value.ToString(), account.Currency);
            processed++;
        }

        Log.ScanCompleted(logger, processed);
        return processed;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Credit expiration scan started at {Timestamp}")]
        public static partial void ScanStarted(ILogger logger, DateTimeOffset timestamp);

        [LoggerMessage(Level = LogLevel.Information, Message = "Credit expiration scan completed, {Count} credits expired")]
        public static partial void ScanCompleted(ILogger logger, int count);
    }
}
