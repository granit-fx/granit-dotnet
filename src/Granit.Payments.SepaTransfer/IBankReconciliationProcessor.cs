using Granit.Payments.SepaTransfer.Domain;

namespace Granit.Payments.SepaTransfer;

/// <summary>
/// Reconciles bank statement entries against pending payment transactions.
/// </summary>
public interface IBankReconciliationProcessor
{
    /// <summary>
    /// Processes a list of bank statement entries, matching them to pending
    /// transactions via structured reference. Matched transactions are updated
    /// to <c>Succeeded</c>.
    /// </summary>
    Task<ReconciliationResult> ProcessAsync(
        IReadOnlyList<BankStatementEntry> entries,
        CancellationToken cancellationToken = default);
}
