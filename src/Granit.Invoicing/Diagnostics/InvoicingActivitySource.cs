using System.Diagnostics;

namespace Granit.Invoicing.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Invoicing distributed tracing.
/// </summary>
internal static class InvoicingActivitySource
{
    internal const string Name = "Granit.Invoicing";

    internal static readonly ActivitySource Source = new(Name);

    internal const string CreateInvoice = "invoicing.create";
    internal const string FinalizeInvoice = "invoicing.finalize";
    internal const string RecordPayment = "invoicing.record_payment";
    internal const string VoidInvoice = "invoicing.void";
    internal const string IssueCreditNote = "invoicing.issue_credit_note";
}
