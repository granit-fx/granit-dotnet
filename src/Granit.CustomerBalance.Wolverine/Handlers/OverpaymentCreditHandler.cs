using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.Guids;
using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.CustomerBalance.Wolverine.Handlers;

/// <summary>
/// Credits overpayment surplus to the tenant's balance account.
/// Creates the account if it does not exist for the (TenantId, Currency) pair.
/// </summary>
internal static partial class OverpaymentCreditHandler
{
    public static async Task HandleAsync(
        OverpaymentDetectedEto eto,
        IBalanceAccountReader accountReader,
        IBalanceAccountWriter accountWriter,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant,
        IClock clock,
        CustomerBalanceMetrics metrics,
        ILogger<OverpaymentDetectedEto> logger,
        CancellationToken cancellationToken)
    {
        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.CreditBalance);

        using (currentTenant.Change(eto.TenantId))
        {
            BalanceAccount? account = await accountReader
                .GetByTenantAndCurrencyAsync(eto.TenantId, eto.Currency, cancellationToken)
                .ConfigureAwait(false);

            if (account is null)
            {
                account = BalanceAccount.Create(guidGenerator.Create(), eto.TenantId, eto.Currency);
                await accountWriter.AddAsync(account, cancellationToken).ConfigureAwait(false);
                Log.AccountCreated(logger, eto.TenantId, eto.Currency);

                // Reload to get tracked entity with transactions collection.
                account = await accountReader
                    .GetByTenantAndCurrencyAsync(eto.TenantId, eto.Currency, cancellationToken)
                    .ConfigureAwait(false);
            }

            account!.Credit(
                eto.OverpaymentAmount,
                TransactionSource.Overpayment,
                "Overpayment on invoice",
                clock.Now,
                guidGenerator.Create(),
                referenceId: eto.InvoiceId,
                referenceType: "Invoice");

            await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
            metrics.RecordCredited(eto.TenantId.ToString(), eto.Currency, TransactionSource.Overpayment.ToString());
            Log.OverpaymentCredited(logger, eto.InvoiceId, eto.OverpaymentAmount, account.Balance);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Overpayment of {Amount} credited for invoice {InvoiceId}, new balance: {NewBalance}")]
        public static partial void OverpaymentCredited(ILogger logger, Guid invoiceId, decimal amount, decimal newBalance);

        [LoggerMessage(Level = LogLevel.Information, Message = "Created balance account for tenant {TenantId} ({Currency})")]
        public static partial void AccountCreated(ILogger logger, Guid tenantId, string currency);
    }
}
