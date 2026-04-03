using Granit.Payments.Domain;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Maps Stripe API status strings to Granit payment enums.
/// </summary>
internal static class StripeStatusMapper
{
    /// <summary>Maps Stripe PaymentIntent status to <see cref="ProviderChargeStatus"/>.</summary>
    public static ProviderChargeStatus MapChargeStatus(string status) => status switch
    {
        "succeeded" => ProviderChargeStatus.Succeeded,
        "processing" => ProviderChargeStatus.Processing,
        "requires_action" or "requires_confirmation" => ProviderChargeStatus.RequiresAction,
        _ => ProviderChargeStatus.Failed,
    };

    /// <summary>Maps Stripe PaymentIntent status to <see cref="PaymentStatus"/>.</summary>
    public static PaymentStatus MapPaymentStatus(string status) => status switch
    {
        "succeeded" => PaymentStatus.Succeeded,
        "processing" => PaymentStatus.Processing,
        "requires_action" or "requires_confirmation" => PaymentStatus.RequiresAction,
        "canceled" => PaymentStatus.Canceled,
        _ => PaymentStatus.Failed,
    };

    /// <summary>Maps Stripe Refund status to <see cref="RefundStatus"/>.</summary>
    public static RefundStatus MapRefundStatus(string status) => status switch
    {
        "succeeded" => RefundStatus.Succeeded,
        "pending" => RefundStatus.Pending,
        _ => RefundStatus.Failed,
    };
}
