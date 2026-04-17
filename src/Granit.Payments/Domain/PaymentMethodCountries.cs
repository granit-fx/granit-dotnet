using System.Collections.Immutable;

namespace Granit.Payments.Domain;

/// <summary>
/// Well-known country sets shared across payment method capability declarations.
/// </summary>
public static class PaymentMethodCountries
{
    /// <summary>
    /// SEPA zone as of 2026: EU-27 plus EEA (IS, LI, NO), Switzerland, United Kingdom,
    /// and the micro-states (Monaco, San Marino, Andorra, Vatican City).
    /// </summary>
    public static ImmutableHashSet<string> SepaZone { get; } = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        // EU-27
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI",
        "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT",
        "NL", "PL", "PT", "RO", "SE", "SI", "SK",
        // EEA
        "IS", "LI", "NO",
        // Switzerland + UK
        "CH", "GB",
        // Micro-states
        "AD", "MC", "SM", "VA");
}
