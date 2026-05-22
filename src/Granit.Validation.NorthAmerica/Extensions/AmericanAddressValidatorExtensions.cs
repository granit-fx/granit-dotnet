using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.NorthAmerica.Internal.UnitedStates;

namespace Granit.Validation.NorthAmerica.Extensions;

/// <summary>
/// FluentValidation extension methods for United States address and contact identifiers.
/// </summary>
public static class AmericanAddressValidatorExtensions
{
    /// <summary>
    /// Validates a US ZIP code.
    /// </summary>
    /// <remarks>
    /// Accepts 5-digit ZIP (e.g. 10001) and ZIP+4 (e.g. 10001-1234).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> UsZipCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(ZipCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidUsZipCode");

    /// <summary>
    /// Validates a NANP (North American Numbering Plan) phone number.
    /// </summary>
    /// <remarks>
    /// Format: NXX-NXX-XXXX where N = 2–9.
    /// Accepts various formatting (parentheses, dashes, dots, spaces) and optional +1 prefix.
    /// Covers US, Canada, and Caribbean NANP countries.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> NanpPhoneNumber<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(NanpPhoneAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:InvalidNanpPhoneNumber");
}
