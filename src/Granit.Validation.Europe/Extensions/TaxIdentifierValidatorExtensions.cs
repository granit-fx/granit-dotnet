using FluentValidation;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for tax identification numbers (VAT numbers).
/// </summary>
public static class TaxIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a Belgian VAT number (numéro de TVA / BTW-nummer).
    /// </summary>
    /// <remarks>
    /// Format: <c>BE</c> followed by a BCE enterprise number (10 digits, leading 0 optional).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianVat<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => EuropeanVatAlgorithm.IsValid(value))
            .WithErrorCodeAndMessage("Validation:Format:BelgianVat");

    /// <summary>
    /// Validates a French VAT number (numéro de TVA intracommunautaire).
    /// </summary>
    /// <remarks>
    /// Format: <c>FR</c> + 2-digit numeric key + 9-digit SIREN.
    /// Key = <c>(12 + 3 × (SIREN mod 97)) mod 97</c>, zero-padded to 2 digits.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchVat<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(FrenchVatAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:FrenchVat");

    /// <summary>
    /// Validates a European VAT number for any EU member state.
    /// </summary>
    /// <remarks>
    /// Dispatches to country-specific validators based on the 2-character country prefix.
    /// Applies algorithmic validation for FR and BE; format validation for all other EU states.
    /// Greece uses the <c>EL</c> prefix in EU VAT context. Returns <see langword="false"/>
    /// for unrecognised country codes.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> EuropeanVat<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(EuropeanVatAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:EuropeanVat");
}
