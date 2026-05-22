using FluentValidation;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for pan-European identifiers.
/// </summary>
public static class EuropeanIdentifierValidatorExtensions
{
    /// <summary>
    /// Validates an EORI (Economic Operators Registration and Identification) number.
    /// </summary>
    /// <remarks>
    /// An EORI number is required for customs operations in the EU. Format: 2-letter
    /// ISO 3166-1 alpha-2 country code followed by up to 15 alphanumeric characters.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Eori<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(EoriAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidEori");
}
