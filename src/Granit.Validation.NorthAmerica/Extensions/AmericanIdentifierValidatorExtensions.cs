using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.NorthAmerica.Internal.UnitedStates;

namespace Granit.Validation.NorthAmerica.Extensions;

/// <summary>
/// FluentValidation extension methods for United States identifiers.
/// </summary>
public static class AmericanIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a US Social Security Number (SSN).
    /// </summary>
    /// <remarks>
    /// Accepts 9 digits with optional dashes (AAA-GG-SSSS).
    /// Rejects invalid area numbers (000, 666, 900–999), all-zero groups, and all-zero serials.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SocialSecurityNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SsnAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidUsSsn");

    /// <summary>
    /// Validates a US Employer Identification Number (EIN).
    /// </summary>
    /// <remarks>
    /// Accepts 9 digits with optional dash (XX-XXXXXXX).
    /// Validates the 2-digit IRS campus prefix.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Ein<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(EinAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidUsEin");

    /// <summary>
    /// Validates a USPS state or territory code (2-letter abbreviation).
    /// </summary>
    /// <remarks>
    /// Accepts 50 states, DC, US territories (AS, GU, MP, PR, VI, UM),
    /// and armed forces codes (AA, AE, AP). Case-insensitive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> UsStateCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(UsStateCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidUsStateCode");
}
