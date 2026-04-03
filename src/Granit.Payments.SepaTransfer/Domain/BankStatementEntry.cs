namespace Granit.Payments.SepaTransfer.Domain;

/// <summary>
/// A parsed entry from a bank statement (CAMT.053, MT940, or CSV).
/// </summary>
public sealed record BankStatementEntry(
    DateTimeOffset BookingDate,
    decimal Amount,
    string Currency,
    string? DebtorName,
    string? DebtorIban,
    string? StructuredReference,
    string? UnstructuredReference,
    string? EndToEndId);
