using System.Collections.Immutable;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;

namespace Granit.Payments.Mollie.Internal;

/// <summary>
/// Static catalog of Mollie payment methods with their capability metadata.
/// </summary>
/// <remarks>
/// <para>
/// Mollie's <c>GET /v2/methods</c> endpoint does not expose per-method country lists,
/// so we maintain them here from the Mollie documentation. Amount bounds come from
/// Mollie's public method reference — a future iteration can replace this with a live
/// call to <c>IMethodClient.GetMethodListAsync(include: "pricing")</c> to pick up
/// bound updates automatically.
/// </para>
/// <para>
/// Source: <c>https://docs.mollie.com/reference/v2/methods-api/list-methods</c> and
/// per-method documentation pages (checked April 2026).
/// </para>
/// </remarks>
internal static class MollieCatalog
{
    private static readonly ImmutableDictionary<string, PaymentMethodAmountBound> NoBounds =
        ImmutableDictionary<string, PaymentMethodAmountBound>.Empty;

    private static readonly ImmutableHashSet<string> GlobalCurrencies =
        ImmutableHashSet<string>.Empty;

    private static readonly ImmutableHashSet<string> Global =
        ImmutableHashSet<string>.Empty;

    private const PaymentMethodSequenceType AllSequences =
        PaymentMethodSequenceType.OneOff | PaymentMethodSequenceType.First | PaymentMethodSequenceType.Recurring;

    /// <summary>Ordered catalog entries covering Mollie's European surface.</summary>
    public static IReadOnlyList<PaymentMethodCatalogEntry> Entries { get; } =
    [
        // Cards — global, all currencies, all sequences
        new(PaymentMethods.Card, PaymentMethodCategory.Card, "Card", new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        // Bank redirects — typically OneOff | First (mandate setup possible), per-country
        new(PaymentMethods.Bancontact, PaymentMethodCategory.BankRedirect, "Bancontact",
            new(Set("BE"), PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceType.OneOff | PaymentMethodSequenceType.First, NoBounds)),

        new(PaymentMethods.Ideal, PaymentMethodCategory.BankRedirect, "iDEAL",
            new(Set("NL"), PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceType.OneOff | PaymentMethodSequenceType.First, NoBounds)),

        new(PaymentMethods.Eps, PaymentMethodCategory.BankRedirect, "EPS",
            new(Set("AT"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.Giropay, PaymentMethodCategory.BankRedirect, "Giropay",
            new(Set("DE"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.Przelewy24, PaymentMethodCategory.BankRedirect, "Przelewy24",
            new(Set("PL"), Set("PLN", "EUR"), PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.Twint, PaymentMethodCategory.BankRedirect, "TWINT",
            new(Set("CH"), Set("CHF"), PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.Trustly, PaymentMethodCategory.BankRedirect, "Trustly",
            new(Set("SE", "FI", "EE", "LV", "LT", "DK", "NO", "GB"), GlobalCurrencies,
                PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.MyBank, PaymentMethodCategory.BankRedirect, "MyBank",
            new(Set("IT"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.Belfius, PaymentMethodCategory.BankRedirect, "Belfius Pay Button",
            new(Set("BE"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceType.OneOff, NoBounds)),

        new(PaymentMethods.Kbc, PaymentMethodCategory.BankRedirect, "KBC/CBC Payment Button",
            new(Set("BE"), PaymentMethodCurrencies.EurOnly, PaymentMethodSequenceType.OneOff, NoBounds)),

        // Bank transfer — SEPA zone, EUR, OneOff
        new(PaymentMethods.BankTransfer, PaymentMethodCategory.BankTransfer, "Bank transfer",
            new(PaymentMethodCountries.SepaZone, PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceType.OneOff, NoBounds)),

        // SEPA Direct Debit — mandate-based, First | Recurring
        new(PaymentMethods.SepaDebit, PaymentMethodCategory.BankDebit, "SEPA Direct Debit",
            new(PaymentMethodCountries.SepaZone, PaymentMethodCurrencies.EurOnly,
                PaymentMethodSequenceType.First | PaymentMethodSequenceType.Recurring, NoBounds)),

        // Wallets — global
        new(PaymentMethods.ApplePay, PaymentMethodCategory.Wallet, "Apple Pay",
            new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        new(PaymentMethods.PayPal, PaymentMethodCategory.Wallet, "PayPal",
            new(Global, GlobalCurrencies, AllSequences, NoBounds)),

        // BNPL — country list plus amount bounds (Mollie docs, April 2026)
        new(PaymentMethods.Klarna, PaymentMethodCategory.BuyNowPayLater, "Klarna",
            new(
                Set("AT", "BE", "CH", "CZ", "DE", "DK", "ES", "FI", "FR", "GB", "IE", "IT", "NL", "NO", "PL", "PT", "SE", "US"),
                Set("EUR", "GBP", "SEK", "DKK", "NOK", "CHF", "USD"),
                PaymentMethodSequenceType.OneOff,
                Bounds(
                    ("EUR", 1m, 10_000m),
                    ("GBP", 1m, 10_000m),
                    ("SEK", 10m, 100_000m),
                    ("DKK", 10m, 75_000m),
                    ("NOK", 10m, 100_000m),
                    ("CHF", 1m, 10_000m),
                    ("USD", 1m, 10_000m)))),

        new(PaymentMethods.Riverty, PaymentMethodCategory.BuyNowPayLater, "Riverty",
            new(
                Set("AT", "CH", "DE", "NL"),
                Set("EUR", "CHF"),
                PaymentMethodSequenceType.OneOff,
                Bounds(
                    ("EUR", 5m, 1_500m),
                    ("CHF", 5m, 1_500m)))),

        // Vouchers — country-specific
        new(PaymentMethods.Paysafecard, PaymentMethodCategory.Voucher, "Paysafecard",
            new(Global, Set("EUR", "USD"), PaymentMethodSequenceType.OneOff,
                Bounds(("EUR", 1m, 1_000m), ("USD", 1m, 1_000m)))),
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
