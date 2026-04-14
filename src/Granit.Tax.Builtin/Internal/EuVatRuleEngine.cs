using Granit.Tax.Internal;

namespace Granit.Tax.Builtin.Internal;

/// <summary>
/// Pure classification engine for EU VAT rules.
/// </summary>
/// <remarks>
/// Determines the <see cref="TransactionType"/>, applicable rate country,
/// and <see cref="TaxExemptionReason"/> based on seller/buyer locations
/// and VAT validation status. Stateless — no I/O, no DI dependencies.
/// </remarks>
internal static class EuVatRuleEngine
{
    /// <summary>Classifies a transaction for EU VAT purposes.</summary>
    /// <param name="sellerCountry">Seller's ISO 3166-1 alpha-2 country code.</param>
    /// <param name="buyerCountry">Buyer's ISO 3166-1 alpha-2 country code.</param>
    /// <param name="buyerHasValidVat">Whether the buyer's VAT number is validated (VIES or offline).</param>
    /// <param name="ossEnabled">Whether the seller has OSS enabled.</param>
    /// <param name="ossCountries">Countries where the seller is OSS-registered (null = all EU if OSS enabled).</param>
    public static TaxCalculationContext Classify(
        string sellerCountry,
        string buyerCountry,
        bool buyerHasValidVat,
        bool ossEnabled,
        IReadOnlySet<string>? ossCountries)
    {
        bool buyerInEu = EuCountries.IsEuMember(buyerCountry);

        // 1. Buyer outside EU → Export (0%)
        if (!buyerInEu)
        {
            return new TaxCalculationContext(
                TransactionType.Export,
                IsBusinessToBusiness: buyerHasValidVat,
                Exemption: TaxExemptionReason.Export,
                RateCountryCode: sellerCountry);
        }

        // Normalize Greece (EL → GR for comparison)
        string normalizedSeller = NormalizeGreece(sellerCountry);
        string normalizedBuyer = NormalizeGreece(buyerCountry);

        // 2. Same country → Domestic (seller country rate)
        if (string.Equals(normalizedSeller, normalizedBuyer, StringComparison.OrdinalIgnoreCase))
        {
            return new TaxCalculationContext(
                TransactionType.DomesticSale,
                IsBusinessToBusiness: buyerHasValidVat,
                Exemption: TaxExemptionReason.None,
                RateCountryCode: sellerCountry);
        }

        // 3. Cross-border EU, B2B with valid VAT → Reverse charge (0%)
        if (buyerHasValidVat)
        {
            return new TaxCalculationContext(
                TransactionType.IntraCommunityB2B,
                IsBusinessToBusiness: true,
                Exemption: TaxExemptionReason.ReverseCharge,
                RateCountryCode: buyerCountry);
        }

        // 4. Cross-border EU, B2C
        if (ossEnabled && IsOssRegistered(buyerCountry, ossCountries))
        {
            // OSS: buyer country rate
            return new TaxCalculationContext(
                TransactionType.IntraCommunityB2C,
                IsBusinessToBusiness: false,
                Exemption: TaxExemptionReason.None,
                RateCountryCode: buyerCountry);
        }

        // No OSS: seller country rate
        return new TaxCalculationContext(
            TransactionType.IntraCommunityB2C,
            IsBusinessToBusiness: false,
            Exemption: TaxExemptionReason.None,
            RateCountryCode: sellerCountry);
    }

    private static bool IsOssRegistered(string buyerCountry, IReadOnlySet<string>? ossCountries) =>
        ossCountries is null || ossCountries.Count == 0 || ossCountries.Contains(buyerCountry);

    private static string NormalizeGreece(string countryCode) =>
        string.Equals(countryCode, "EL", StringComparison.OrdinalIgnoreCase) ? "GR" : countryCode;
}
