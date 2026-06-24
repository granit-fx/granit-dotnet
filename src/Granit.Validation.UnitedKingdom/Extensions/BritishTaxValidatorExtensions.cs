using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.UnitedKingdom.Internal;

namespace Granit.Validation.UnitedKingdom.Extensions;

/// <summary>
/// FluentValidation extension methods for United Kingdom tax and company identifiers.
/// </summary>
public static class BritishTaxValidatorExtensions
{
    /// <summary>
    /// Validates an HMRC Unique Taxpayer Reference (UTR).
    /// </summary>
    /// <remarks>
    /// 10-digit number. The first digit is a MOD 11 check digit calculated from digits 2–10.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> UniqueTaxpayerReference<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(UtrAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkUtr");

    /// <summary>
    /// Validates a UK VAT registration number.
    /// </summary>
    /// <remarks>
    /// Format: GB + 9 or 12 digits. Check digits validated using MOD 97 algorithm.
    /// Also accepts GD (government) and HA (health authority) prefixes.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> UkVat<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(UkVatAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkVat");

    /// <summary>
    /// Validates a Companies House registration number.
    /// </summary>
    /// <remarks>
    /// 8 characters: all digits, or 2-letter prefix (OC, SC, NI, etc.) + 6 digits.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> CompaniesHouseNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(CompaniesHouseNumberAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkCompaniesHouseNumber");
}
