using FluentValidation;
using Granit.Validation.Europe.Internal.Netherlands;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for Dutch identifiers and address fields.
/// </summary>
public static class DutchIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a Dutch Citizen Service Number (Burgerservicenummer / BSN).
    /// </summary>
    /// <remarks>
    /// Expects exactly 9 digits with a valid elfproef (eleven-test).
    /// Spaces and dashes are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> DutchBsn<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BsnAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:DutchBsn");

    /// <summary>
    /// Validates a Dutch Chamber of Commerce number (Kamer van Koophandel / KVK).
    /// </summary>
    /// <remarks>
    /// Expects exactly 8 digits. Spaces are stripped before validation.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> DutchKvk<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(KvkAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:DutchKvk");

    /// <summary>
    /// Validates a Dutch postcode.
    /// </summary>
    /// <remarks>
    /// Format: 4 digits (1000–9999) + optional space + 2 uppercase letters.
    /// The letter combinations SA, SD, and SS are excluded per Dutch postal convention.
    /// Input is normalised to uppercase: <c>1234 ab</c> is accepted.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> DutchPostcode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(DutchPostcodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:DutchPostcode");
}
