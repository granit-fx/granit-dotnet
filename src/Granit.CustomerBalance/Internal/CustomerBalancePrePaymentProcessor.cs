using System.Diagnostics;
using Granit.CustomerBalance.Diagnostics;
using Granit.CustomerBalance.Domain;
using Granit.Guids;
using Granit.Invoicing;
using Granit.Invoicing.Events;
using Granit.Parties.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.CustomerBalance.Internal;

/// <summary>
/// Pre-payment processor that deducts available credit from the tenant's balance
/// before the PSP charges the remaining amount. Idempotent via <c>ReferenceId</c>
/// deduplication — safe for messaging retries.
/// </summary>
internal sealed partial class CustomerBalancePrePaymentProcessor(
    IBalanceAccountReader accountReader,
    IBalanceAccountWriter accountWriter,
    IInvoiceCreditApplier invoiceCreditApplier,
    IGuidGenerator guidGenerator,
    IClock clock,
    CustomerBalanceMetrics metrics,
    ILogger<CustomerBalancePrePaymentProcessor> logger) : IInvoicePrePaymentProcessor
{
    public async Task<PrePaymentResult> ProcessAsync(
        InvoiceFinalizedEto eto, CancellationToken cancellationToken = default)
    {
        using Activity? activity = CustomerBalanceActivitySource.Source
            .StartActivity(CustomerBalanceActivitySource.DebitBalance);

        BalanceAccount? account = await accountReader
            .GetByPartyAndCurrencyAsync(PartyId.Create(eto.PartyId), eto.Currency, cancellationToken)
            .ConfigureAwait(false);

        if (account is null || account.Balance <= 0)
        {
            return new PrePaymentResult(eto.Total);
        }

        // Idempotency: check if we already debited for this invoice (messaging retry).
        BalanceTransaction? existingDeduction = account.Transactions
            .FirstOrDefault(t =>
                t.ReferenceId == eto.InvoiceId &&
                t.Source == TransactionSource.InvoiceDeduction);

        decimal deduction;

        if (existingDeduction is not null)
        {
            deduction = existingDeduction.Amount;
            Log.IdempotentDebitSkipped(logger, eto.InvoiceId, deduction);
        }
        else
        {
            deduction = Math.Min(account.Balance, eto.Total);
            account.Debit(
                deduction,
                TransactionSource.InvoiceDeduction,
                "Balance deduction",
                clock.Now,
                guidGenerator.Create(),
                referenceId: eto.InvoiceId,
                referenceType: "Invoice");

            await accountWriter.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
            metrics.RecordDebited(eto.TenantId.ToString(), eto.Currency, TransactionSource.InvoiceDeduction.ToString());
            Log.BalanceDebited(logger, eto.InvoiceId, deduction, account.Balance);
        }

        await invoiceCreditApplier
            .ApplyCreditAsync(eto.InvoiceId, deduction, cancellationToken)
            .ConfigureAwait(false);

        return new PrePaymentResult(eto.Total - deduction);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Balance debited {Amount} for invoice {InvoiceId}, remaining balance: {RemainingBalance}")]
        public static partial void BalanceDebited(ILogger logger, Guid invoiceId, decimal amount, decimal remainingBalance);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Idempotent debit skipped for invoice {InvoiceId}, existing deduction: {Amount}")]
        public static partial void IdempotentDebitSkipped(ILogger logger, Guid invoiceId, decimal amount);
    }
}
