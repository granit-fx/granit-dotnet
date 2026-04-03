using Granit.Payments.Domain;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Maps Granit <see cref="PaymentMethodType"/> to Mollie payment method strings.
/// </summary>
internal static class MolliePaymentMethodTypeMapper
{
    /// <summary>Returns the Mollie payment method string.</summary>
    public static string? ToMollieMethod(PaymentMethodType methodType) => methodType switch
    {
        PaymentMethodType.Card => "creditcard",
        PaymentMethodType.SepaDebit => "directdebit",
        PaymentMethodType.BankTransfer => "banktransfer",
        PaymentMethodType.Ideal => "ideal",
        PaymentMethodType.Bancontact => "bancontact",
        _ => null,
    };

    /// <summary>Maps Mollie method string back to Granit enum.</summary>
    public static PaymentMethodType FromMollieMethod(string method) => method switch
    {
        "creditcard" => PaymentMethodType.Card,
        "directdebit" => PaymentMethodType.SepaDebit,
        "banktransfer" => PaymentMethodType.BankTransfer,
        "ideal" => PaymentMethodType.Ideal,
        "bancontact" => PaymentMethodType.Bancontact,
        _ => PaymentMethodType.Card,
    };
}
