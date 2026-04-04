namespace Granit.Invoicing.Domain.ValueObjects;

/// <summary>
/// Groups the parent invoice reference and reason for a credit note.
/// </summary>
/// <param name="ParentInvoiceId">Identifier of the parent invoice being credited.</param>
/// <param name="Reason">Human-readable reason for the credit note.</param>
public sealed record CreditNoteInfo(InvoiceId ParentInvoiceId, string Reason);
