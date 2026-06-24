using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.NorthAmerica.Internal.Canada;

namespace Granit.Validation.NorthAmerica.Extensions;

/// <summary>
/// FluentValidation extension methods for Canadian address identifiers.
/// </summary>
public static class CanadianAddressValidatorExtensions
{
    /// <summary>
    /// Validates a Canadian postal code.
    /// </summary>
    /// <remarks>
    /// Format: A1A 1A1 (letter-digit-letter space digit-letter-digit).
    /// Space is optional. Letters D, F, I, O, Q, U are excluded.
    /// W and Z are excluded as the first character.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> CanadianPostalCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(CanadianPostalCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:CanadianPostalCode");
}
