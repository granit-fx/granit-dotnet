using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.CustomerBalance.Internal;

/// <summary>Default implementation of <see cref="IAdminDebitService"/>.</summary>
internal sealed partial class DefaultAdminDebitService(
    IBalanceAccountReader accountReader,
    IBalanceAccountWriter accountWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    CustomerBalanceMetrics metrics,
    ILogger<DefaultAdminDebitService> logger) : IAdminDebitService
{
    public async Task<BalanceAccount> DebitAsync(
        Guid tenantId,
        decimal amount,
        string currency,
        string reason,
        Guid? referenceId = null,
        string? referenceType = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.DebitBalance);

        BalanceAccount? account = await accountReader
            .GetByTenantAndCurrencyAsync(tenantId, currency, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"No balance account exists for tenant '{tenantId}' in currency '{currency}'.");

        // Idempotency: if a previous ManualAdjustment debit with the same
        // referenceId already landed, return the account unchanged. Mirrors the
        // (ReferenceId, Source) lookup used by CustomerBalancePrePaymentProcessor.
        if (referenceId is { } refId)
        {
            BalanceTransaction? existing = account.Transactions
                .FirstOrDefault(t =>
                    t.Type == TransactionType.Debit
                    && t.Source == TransactionSource.ManualAdjustment
                    && t.ReferenceId == refId);

            if (existing is not null)
            {
                Log.IdempotentDebitSkipped(logger, refId, existing.Amount);
                return account;
            }
        }

        // Domain throws InsufficientBalanceException on under-balance; let it bubble.
        account.Debit(
            amount,
            TransactionSource.ManualAdjustment,
            reason,
            clock.Now,
            guidGenerator.Create(),
            referenceId: referenceId,
            referenceType: referenceType);

        await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        metrics.RecordDebited(tenantId.ToString(), currency, TransactionSource.ManualAdjustment.ToString());
        Log.AdminDebited(logger, amount, account.Balance);

        return account;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Admin debit (ManualAdjustment) of {Amount} applied, new balance: {NewBalance}")]
        public static partial void AdminDebited(ILogger logger, decimal amount, decimal newBalance);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Admin debit skipped (idempotent) for reference {ReferenceId}, original amount: {Amount}")]
        public static partial void IdempotentDebitSkipped(ILogger logger, Guid referenceId, decimal amount);
    }
}
