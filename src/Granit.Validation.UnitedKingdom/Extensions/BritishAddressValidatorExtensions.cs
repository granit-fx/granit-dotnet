using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.UnitedKingdom.Internal;

namespace Granit.Validation.UnitedKingdom.Extensions;

/// <summary>
/// FluentValidation extension methods for United Kingdom address identifiers.
/// </summary>
public static class BritishAddressValidatorExtensions
{
    /// <summary>
    /// Validates a UK postcode.
    /// </summary>
    /// <remarks>
    /// Accepts all standard UK postcode formats (A9 9AA through AA9A 9AA).
    /// Space between outward and inward codes is optional. Case-insensitive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> UkPostcode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(UkPostcodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkPostcode");
}
