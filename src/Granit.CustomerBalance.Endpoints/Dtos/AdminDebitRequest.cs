namespace Granit.CustomerBalance.Endpoints.Dtos;

/// <summary>
/// Request to debit a tenant's <c>BalanceAccount</c> manually — admin tooling
/// for corrections, scheduled drawdowns, and non-invoice adjustments.
/// </summary>
/// <param name="Amount">Amount to debit (must be > 0).</param>
/// <param name="Currency">ISO 4217 currency code identifying the target balance account.</param>
/// <param name="Reason">Free-text justification (audit trail; ≤ 500 chars; no PII per GDPR).</param>
/// <param name="ReferenceId">
/// Optional external document reference. When present, a previous debit with the
/// same <c>(ReferenceId, Source = ManualAdjustment)</c> short-circuits the call —
/// admin retries are safe.
/// </param>
/// <param name="ReferenceType">Type of the referenced document (e.g. <c>"AdminAdjustment"</c>).</param>
public sealed record AdminDebitRequest(
    decimal Amount,
    string Currency,
    string Reason,
    Guid? ReferenceId = null,
    string? ReferenceType = null);
