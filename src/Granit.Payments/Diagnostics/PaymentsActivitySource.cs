using System.Diagnostics;

namespace Granit.Payments.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Payments distributed tracing.
/// </summary>
internal static class PaymentsActivitySource
{
    internal const string Name = "Granit.Payments";

    internal static readonly ActivitySource Source = new(Name);

    internal const string InitiatePayment = "payments.initiate";
    internal const string ProcessWebhook = "payments.process_webhook";
    internal const string RequestRefund = "payments.request_refund";
    internal const string CompleteRefund = "payments.complete_refund";
    internal const string OpenDispute = "payments.open_dispute";
}
