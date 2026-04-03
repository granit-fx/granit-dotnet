using Granit.Payments.Domain;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Maps Granit payment method identifiers to Mollie payment method strings.
/// </summary>
internal static class MolliePaymentMethodTypeMapper
{
    /// <summary>Returns the Mollie payment method string.</summary>
    public static string? ToMollieMethod(string methodType) => methodType switch
    {
        PaymentMethods.Card => "creditcard",
        PaymentMethods.SepaDebit => "directdebit",
        PaymentMethods.BankTransfer => "banktransfer",
        PaymentMethods.Ideal => "ideal",
        PaymentMethods.Bancontact => "bancontact",
        PaymentMethods.Eps => "eps",
        PaymentMethods.Blik => "blik",
        PaymentMethods.Przelewy24 => "przelewy24",
        PaymentMethods.Trustly => "trustly",
        PaymentMethods.Twint => "twint",
        PaymentMethods.Belfius => "belfius",
        PaymentMethods.Kbc => "kbc",
        PaymentMethods.MyBank => "mybank",
        PaymentMethods.ApplePay => "applepay",
        PaymentMethods.GooglePay => "googlepay",
        PaymentMethods.PayPal => "paypal",
        PaymentMethods.Klarna => "klarna",
        PaymentMethods.Alma => "alma",
        PaymentMethods.Riverty => "riverty",
        PaymentMethods.Paysafecard => "paysafecard",
        _ => null,
    };

    /// <summary>Maps Mollie method string back to Granit identifier.</summary>
    public static string FromMollieMethod(string method) => method switch
    {
        "creditcard" => PaymentMethods.Card,
        "directdebit" or "sepadirectdebit" => PaymentMethods.SepaDebit,
        "banktransfer" => PaymentMethods.BankTransfer,
        "ideal" => PaymentMethods.Ideal,
        "bancontact" => PaymentMethods.Bancontact,
        "eps" => PaymentMethods.Eps,
        "blik" => PaymentMethods.Blik,
        "przelewy24" => PaymentMethods.Przelewy24,
        "trustly" => PaymentMethods.Trustly,
        "twint" => PaymentMethods.Twint,
        "belfius" => PaymentMethods.Belfius,
        "kbc" => PaymentMethods.Kbc,
        "mybank" => PaymentMethods.MyBank,
        "applepay" => PaymentMethods.ApplePay,
        "googlepay" => PaymentMethods.GooglePay,
        "paypal" => PaymentMethods.PayPal,
        "klarna" => PaymentMethods.Klarna,
        "alma" => PaymentMethods.Alma,
        "riverty" => PaymentMethods.Riverty,
        "paysafecard" => PaymentMethods.Paysafecard,
        _ => method, // Pass through unknown types
    };
}
