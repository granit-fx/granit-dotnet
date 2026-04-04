using Granit.Payments.Domain;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Maps Mollie payment status strings to Granit payment enums.
/// </summary>
internal static class MollieStatusMapper
{
    /// <summary>Maps Mollie payment status to <see cref="ProviderChargeStatus"/>.</summary>
    public static ProviderChargeStatus MapChargeStatus(string status) => status switch
    {
        "paid" => ProviderChargeStatus.Succeeded,
        "pending" => ProviderChargeStatus.Processing,
        "open" => ProviderChargeStatus.RequiresAction,
        "authorized" => ProviderChargeStatus.Succeeded,
        _ => ProviderChargeStatus.Failed,
    };

    /// <summary>Maps Mollie payment status to <see cref="PaymentStatus"/>.</summary>
    public static PaymentStatus MapPaymentStatus(string status) => status switch
    {
        "paid" => PaymentStatus.Succeeded,
        "pending" => PaymentStatus.Processing,
        "open" => PaymentStatus.RequiresAction,
        "authorized" => PaymentStatus.Succeeded,
        "canceled" => PaymentStatus.Canceled,
        "expired" => PaymentStatus.Failed,
        "failed" => PaymentStatus.Failed,
        _ => PaymentStatus.Failed,
    };

    /// <summary>Maps Mollie refund status to <see cref="RefundStatus"/>.</summary>
    public static RefundStatus MapRefundStatus(string status) => status switch
    {
        "refunded" => RefundStatus.Succeeded,
        "pending" or "processing" or "queued" => RefundStatus.Pending,
        _ => RefundStatus.Failed,
    };
}
