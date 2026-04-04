using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.Guids;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.CustomerBalance.Internal;

/// <summary>
/// Default implementation of <see cref="IOverpaymentCreditService"/>.
/// Credits overpayment surplus to the tenant's balance account.
/// Creates the account if it does not exist for the (TenantId, Currency) pair.
/// </summary>
internal sealed partial class DefaultOverpaymentCreditService(
    IBalanceAccountReader accountReader,
    IBalanceAccountWriter accountWriter,
    IGuidGenerator guidGenerator,
    IClock clock,
    CustomerBalanceMetrics metrics,
    ILogger<DefaultOverpaymentCreditService> logger) : IOverpaymentCreditService
{
    public async Task CreditOverpaymentAsync(
        Guid tenantId,
        string currency,
        decimal amount,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.CreditBalance);

        BalanceAccount? account = await accountReader
            .GetByTenantAndCurrencyAsync(tenantId, currency, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            account = BalanceAccount.Create(guidGenerator.Create(), tenantId, currency);
            await accountWriter.AddAsync(account, cancellationToken).ConfigureAwait(false);
            Log.AccountCreated(logger, tenantId, currency);

            // Reload to get tracked entity with transactions collection.
            account = await accountReader
                .GetByTenantAndCurrencyAsync(tenantId, currency, cancellationToken)
                .ConfigureAwait(false);
        }

        account!.Credit(
            amount,
            TransactionSource.Overpayment,
            "Overpayment on invoice",
            clock.Now,
            guidGenerator.Create(),
            referenceId: invoiceId,
            referenceType: "Invoice");

        await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        metrics.RecordCredited(tenantId.ToString(), currency, TransactionSource.Overpayment.ToString());
        Log.OverpaymentCredited(logger, invoiceId, amount, account.Balance);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Overpayment of {Amount} credited for invoice {InvoiceId}, new balance: {NewBalance}")]
        public static partial void OverpaymentCredited(ILogger logger, Guid invoiceId, decimal amount, decimal newBalance);

        [LoggerMessage(Level = LogLevel.Information, Message = "Created balance account for tenant {TenantId} ({Currency})")]
        public static partial void AccountCreated(ILogger logger, Guid tenantId, string currency);
    }
}
