using System.Collections.Immutable;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Static catalog of Stripe payment methods with their capability metadata.
/// </summary>
/// <remarks>
/// <para>
/// Sourced from Stripe's payment method support matrix
/// (<c>https://docs.stripe.com/payments/payment-methods/payment-method-support</c>,
/// checked April 2026). Country lists are restricted to the European surface we actively
/// support; Stripe's full catalog spans additional regions not wired into Granit yet.
/// </para>
/// <para>
/// A future enhancement can call <c>PaymentMethodConfigurationService.GetAsync()</c> to
/// honor the merchant's current dashboard toggles (<c>display_preference.value == "on"</c>)
/// and override these defaults dynamically.
/// </para>
/// </remarks>
internal static class StripeCatalog
{
    private static readonly ImmutableDictionary<string, PaymentMethodAmountBound> NoBounds =
        ImmutableDictionary<string, PaymentMethodAmountBound>.Empty;

    private static readonly ImmutableHashSet<string> GlobalCurrencies = ImmutableHashSet<string>.Empty;
    private static readonly ImmutableHashSet<string> Global = ImmutableHashSet<string>.Empty;

    private const PaymentMethodSequenceTypes AllSequences =
        PaymentMethodSequenceTypes.OneOff | PaymentMethodSequenceTypes.First | PaymentMethodSequenceTypes.Recurring;

    /// <summary>Ordered catalog covering Stripe's European surface.</summary>
    public static IReadOnlyList<PaymentMethodCatalogEntry> Entries { get; } =
    [
        new(PaymentMethods.Card, PaymentMethodCategory.Card, "Card",
            new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        new(PaymentMethods.Bancontact, PaymentMethodCategory.BankRedirect, "Bancontact",
            new(Set("BE"), PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceTypes.OneOff | PaymentMethodSequenceTypes.First, NoBounds)),

        new(PaymentMethods.Ideal, PaymentMethodCategory.BankRedirect, "iDEAL",
            new(Set("NL"), PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceTypes.OneOff | PaymentMethodSequenceTypes.First, NoBounds)),

        new(PaymentMethods.Eps, PaymentMethodCategory.BankRedirect, "EPS",
            new(Set("AT"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.Giropay, PaymentMethodCategory.BankRedirect, "Giropay",
            new(Set("DE"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.Przelewy24, PaymentMethodCategory.BankRedirect, "Przelewy24",
            new(Set("PL"), Set("PLN", "EUR"), PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.Blik, PaymentMethodCategory.BankRedirect, "BLIK",
            new(Set("PL"), Set("PLN"), PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.Twint, PaymentMethodCategory.BankRedirect, "TWINT",
            new(Set("CH"), Set("CHF"), PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.Trustly, PaymentMethodCategory.BankRedirect, "Trustly",
            new(Set("SE", "FI", "EE", "LV", "LT", "DK", "NO", "GB"), GlobalCurrencies,
                PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.MyBank, PaymentMethodCategory.BankRedirect, "MyBank",
            new(Set("IT"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.BankTransfer, PaymentMethodCategory.BankTransfer, "Bank transfer",
            new(PaymentMethodCountries.SepaZone, PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.SepaDebit, PaymentMethodCategory.BankDebit, "SEPA Direct Debit",
            new(PaymentMethodCountries.SepaZone, PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceTypes.First | PaymentMethodSequenceTypes.Recurring, NoBounds)),

        new(PaymentMethods.ApplePay, PaymentMethodCategory.Wallet, "Apple Pay",
            new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        new(PaymentMethods.GooglePay, PaymentMethodCategory.Wallet, "Google Pay",
            new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        new(PaymentMethods.PayPal, PaymentMethodCategory.Wallet, "PayPal",
            new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        new(PaymentMethods.Alipay, PaymentMethodCategory.Wallet, "Alipay",
            new(Set("CN", "HK", "SG"), Set("CNY", "USD", "EUR", "GBP", "HKD", "SGD"),
                PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.WechatPay, PaymentMethodCategory.Wallet, "WeChat Pay",
            new(Set("CN", "HK"), Set("CNY", "USD", "EUR", "GBP", "HKD"),
                PaymentMethodSequenceTypes.OneOff, NoBounds)),

        new(PaymentMethods.Klarna, PaymentMethodCategory.BuyNowPayLater, "Klarna",
            new(
                Set("AT", "BE", "CH", "CZ", "DE", "DK", "ES", "FI", "FR", "GB", "IE", "IT", "NL", "NO", "PL", "PT", "SE", "US"),
                Set("EUR", "GBP", "SEK", "DKK", "NOK", "CHF", "USD"),
                PaymentMethodSequenceTypes.OneOff,
                Bounds(
                    ("EUR", 1m, 10_000m),
                    ("GBP", 1m, 10_000m),
                    ("SEK", 10m, 100_000m),
                    ("DKK", 10m, 75_000m),
                    ("NOK", 10m, 100_000m),
                    ("CHF", 1m, 10_000m),
                    ("USD", 1m, 10_000m)))),

        new(PaymentMethods.Riverty, PaymentMethodCategory.BuyNowPayLater, "Afterpay/Clearpay",
            new(
                Set("AT", "CH", "DE", "NL"),
                Set("EUR", "CHF"),
                PaymentMethodSequenceTypes.OneOff,
                Bounds(
                    ("EUR", 5m, 1_500m),
                    ("CHF", 5m, 1_500m)))),
    ];

    private static ImmutableHashSet<string> Set(params string[] values) =>
        ImmutableHashSet.Create(StringComparer.Ordinal, values);

    private static ImmutableDictionary<string, PaymentMethodAmountBound> Bounds(
        params (string Currency, decimal Min, decimal Max)[] entries)
    {
        ImmutableDictionary<string, PaymentMethodAmountBound>.Builder builder =
            ImmutableDictionary.CreateBuilder<string, PaymentMethodAmountBound>(StringComparer.Ordinal);

        foreach ((string currency, decimal min, decimal max) in entries)
        {
            builder.Add(currency, new PaymentMethodAmountBound(currency, min, max));
        }

        return builder.ToImmutable();
    }
}
