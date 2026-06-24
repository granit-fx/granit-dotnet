using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.UnitedKingdom.Internal;

namespace Granit.Validation.UnitedKingdom.Extensions;

/// <summary>
/// FluentValidation extension methods for United Kingdom personal identifiers.
/// </summary>
public static class BritishIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a UK National Insurance (NI) number.
    /// </summary>
    /// <remarks>
    /// Format: 2 letters + 6 digits + 1 letter (A–D). Spaces and dashes are stripped.
    /// Invalid prefixes (BG, GB, NK, KN, TN, NT, ZZ) and restricted first/second letters are rejected.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> NationalInsuranceNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NationalInsuranceNumberAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkNationalInsuranceNumber");

    /// <summary>
    /// Validates an NHS (National Health Service) number.
    /// </summary>
    /// <remarks>
    /// 10-digit number with a MOD 11 check digit (digit 10).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> NhsNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NhsNumberAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkNhsNumber");
}
