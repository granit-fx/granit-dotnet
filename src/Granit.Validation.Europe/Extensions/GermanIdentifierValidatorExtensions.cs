using FluentValidation;
using Granit.Validation.Europe.Internal.Germany;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for German identifiers and address fields.
/// </summary>
public static class GermanIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a German Tax Identification Number (Steuerliche Identifikationsnummer).
    /// </summary>
    /// <remarks>
    /// Expects exactly 11 digits with a valid ISO 7064 MOD 11,10 check digit.
    /// First digit must be non-zero. Among positions 1–10, exactly one digit appears twice
    /// and exactly one digit does not appear. Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> GermanSteuerId<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SteuerIdAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidGermanSteuerId");

    /// <summary>
    /// Validates a German postal code (Postleitzahl / PLZ).
    /// </summary>
    /// <remarks>
    /// Accepts 5-digit codes in the range 01001–99998.
    /// The value <c>00000</c> is not accepted.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> GermanPostalCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(GermanPostalCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidGermanPostalCode");
}
