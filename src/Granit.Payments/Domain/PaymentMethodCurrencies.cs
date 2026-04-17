using System.Collections.Immutable;

namespace Granit.Payments.Domain;

/// <summary>
/// Well-known currency sets shared across payment method capability declarations.
/// </summary>
public static class PaymentMethodCurrencies
{
    /// <summary>Single-currency set: EUR. Used by SEPA-constrained methods.</summary>
    public static ImmutableHashSet<string> EurOnly { get; } =
        ImmutableHashSet.Create(StringComparer.Ordinal, "EUR");
}
