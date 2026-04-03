using Granit.Payments.Domain;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Maps Granit payment method identifiers to Stripe payment_method_types strings.
/// </summary>
internal static class StripePaymentMethodTypeMapper
{
    /// <summary>Returns the Stripe payment method type strings for checkout sessions.</summary>
    public static List<string> ToStripeTypes(string methodType) => methodType switch
    {
        PaymentMethods.Card => ["card"],
        PaymentMethods.SepaDebit => ["sepa_debit"],
        PaymentMethods.Ideal => ["ideal"],
        PaymentMethods.Bancontact => ["bancontact"],
        PaymentMethods.BankTransfer => ["customer_balance"],
        PaymentMethods.ApplePay => ["card"], // Apple Pay uses card payment method type in Stripe
        PaymentMethods.GooglePay => ["card"], // Google Pay uses card payment method type in Stripe
        PaymentMethods.Klarna => ["klarna"],
        PaymentMethods.Eps => ["eps"],
        PaymentMethods.Giropay => ["giropay"],
        PaymentMethods.PayPal => ["paypal"],
        _ => ["card"],
    };

    /// <summary>Maps a Stripe payment method type string back to Granit identifier.</summary>
    public static string FromStripeType(string stripeType) => stripeType switch
    {
        "card" => PaymentMethods.Card,
        "sepa_debit" => PaymentMethods.SepaDebit,
        "ideal" => PaymentMethods.Ideal,
        "bancontact" => PaymentMethods.Bancontact,
        "customer_balance" => PaymentMethods.BankTransfer,
        "klarna" => PaymentMethods.Klarna,
        "eps" => PaymentMethods.Eps,
        "giropay" => PaymentMethods.Giropay,
        "paypal" => PaymentMethods.PayPal,
        _ => stripeType, // Pass through unknown types
    };
}
