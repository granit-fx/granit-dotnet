using Granit.Payments.Domain;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Maps Granit <see cref="PaymentMethodType"/> to Stripe payment_method_types strings.
/// </summary>
internal static class StripePaymentMethodTypeMapper
{
    /// <summary>Returns the Stripe payment method type strings for checkout sessions.</summary>
    public static List<string> ToStripeTypes(PaymentMethodType methodType) => methodType switch
    {
        PaymentMethodType.Card => ["card"],
        PaymentMethodType.SepaDebit => ["sepa_debit"],
        PaymentMethodType.Ideal => ["ideal"],
        PaymentMethodType.Bancontact => ["bancontact"],
        PaymentMethodType.BankTransfer => ["customer_balance"],
        _ => ["card"],
    };

    /// <summary>Maps a Stripe payment method type string back to Granit enum.</summary>
    public static PaymentMethodType FromStripeType(string stripeType) => stripeType switch
    {
        "card" => PaymentMethodType.Card,
        "sepa_debit" => PaymentMethodType.SepaDebit,
        "ideal" => PaymentMethodType.Ideal,
        "bancontact" => PaymentMethodType.Bancontact,
        "customer_balance" => PaymentMethodType.BankTransfer,
        _ => PaymentMethodType.Card,
    };
}
