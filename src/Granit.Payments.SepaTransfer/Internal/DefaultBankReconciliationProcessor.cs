using Granit.Payments.SepaTransfer.Domain;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.SepaTransfer.Internal;

/// <summary>
/// Matches bank statement entries to pending payment transactions via structured reference.
/// </summary>
internal sealed partial class DefaultBankReconciliationProcessor(
    IPaymentTransactionReader transactionReader,
    IPaymentTransactionWriter transactionWriter,
    StructuredReferenceGenerator referenceGenerator,
    IClock clock,
    ILogger<DefaultBankReconciliationProcessor> logger) : IBankReconciliationProcessor
{
    public async Task<ReconciliationResult> ProcessAsync(
        IReadOnlyList<BankStatementEntry> entries,
        CancellationToken cancellationToken = default)
    {
        var matched = new List<ReconciliationMatch>();
        var unmatched = new List<BankStatementEntry>();
        var alreadyProcessed = new List<BankStatementEntry>();

        foreach (BankStatementEntry entry in entries)
        {
            string? refPrefix = referenceGenerator.ExtractPrefix(entry.StructuredReference);

            if (refPrefix is null)
            {
                // Try unstructured reference as fallback
                refPrefix = referenceGenerator.ExtractPrefix(entry.UnstructuredReference);
            }

            if (refPrefix is null)
            {
                unmatched.Add(entry);
                Log.Unmatched(logger, entry.Amount, entry.StructuredReference);
                continue;
            }

            // Find the pending transaction by provider transaction ID pattern
            string providerTxId = $"sepa-{refPrefix.ToLowerInvariant()}";
            Payments.Domain.PaymentTransaction? transaction = await transactionReader
                .GetByProviderTransactionIdAsync("sepa-transfer", providerTxId, cancellationToken)
                .ConfigureAwait(false);

            if (transaction is null)
            {
                unmatched.Add(entry);
                Log.NoTransactionFound(logger, refPrefix);
                continue;
            }

            if (transaction.Status == Payments.Domain.PaymentStatus.Succeeded)
            {
                alreadyProcessed.Add(entry);
                Log.AlreadyReconciled(logger, transaction.Id);
                continue;
            }

            // Mark as succeeded
            transaction.MarkSucceeded(providerTxId, clock.Now);
            await transactionWriter.UpdateAsync(transaction, cancellationToken)
                .ConfigureAwait(false);

            matched.Add(new ReconciliationMatch(entry, transaction.Id, transaction.InvoiceId));
            Log.Matched(logger, transaction.Id, entry.Amount);
        }

        Log.ReconciliationCompleted(logger, matched.Count, unmatched.Count, alreadyProcessed.Count);

        return new ReconciliationResult(matched, unmatched, alreadyProcessed);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Reconciliation matched transaction {TransactionId}: {Amount}")]
        public static partial void Matched(ILogger logger, Guid transactionId, decimal amount);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Unmatched bank entry: {Amount}, reference {Reference}")]
        public static partial void Unmatched(ILogger logger, decimal amount, string? reference);

        [LoggerMessage(Level = LogLevel.Debug, Message = "No transaction found for reference prefix {RefPrefix}")]
        public static partial void NoTransactionFound(ILogger logger, string refPrefix);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Transaction {TransactionId} already reconciled")]
        public static partial void AlreadyReconciled(ILogger logger, Guid transactionId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Reconciliation completed: {Matched} matched, {Unmatched} unmatched, {AlreadyProcessed} already processed")]
        public static partial void ReconciliationCompleted(ILogger logger, int matched, int unmatched, int alreadyProcessed);
    }
}
