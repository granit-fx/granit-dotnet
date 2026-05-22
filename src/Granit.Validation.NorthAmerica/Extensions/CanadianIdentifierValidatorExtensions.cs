using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.NorthAmerica.Internal.Canada;

namespace Granit.Validation.NorthAmerica.Extensions;

/// <summary>
/// FluentValidation extension methods for Canadian identifiers.
/// </summary>
public static class CanadianIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates a Canadian Social Insurance Number (SIN).
    /// </summary>
    /// <remarks>
    /// Accepts 9 digits with optional dashes or spaces (XXX-XXX-XXX or XXX XXX XXX).
    /// Validated using the Luhn algorithm. First digit cannot be 0 or 8.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SocialInsuranceNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SinAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidCanadianSin");

    /// <summary>
    /// Validates a Canadian Business Number (BN / NE).
    /// </summary>
    /// <remarks>
    /// 9-digit number assigned by the CRA, validated using Luhn mod-10.
    /// Accepts with or without dashes/spaces.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> CanadianBusinessNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(BusinessNumberAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidCanadianBusinessNumber");
}
