namespace Granit.Payments.SepaTransfer.Domain;

/// <summary>Result of reconciling a bank statement against pending transactions.</summary>
/// <param name="Matched">Entries matched to a pending payment transaction.</param>
/// <param name="Unmatched">Entries that could not be matched (manual review needed).</param>
/// <param name="AlreadyProcessed">Entries that were already reconciled (duplicate statement).</param>
public sealed record ReconciliationResult(
    IReadOnlyList<ReconciliationMatch> Matched,
    IReadOnlyList<BankStatementEntry> Unmatched,
    IReadOnlyList<BankStatementEntry> AlreadyProcessed);

/// <summary>A successful match between a bank statement entry and a payment transaction.</summary>
/// <param name="Entry">The bank statement entry.</param>
/// <param name="TransactionId">The matched Granit payment transaction ID.</param>
/// <param name="InvoiceId">The associated invoice ID.</param>
public sealed record ReconciliationMatch(
    BankStatementEntry Entry,
    Guid TransactionId,
    Guid InvoiceId);
