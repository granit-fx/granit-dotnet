using FluentValidation;
using Granit.Validation.Extensions;
using Granit.Validation.UnitedKingdom.Internal;

namespace Granit.Validation.UnitedKingdom.Extensions;

/// <summary>
/// FluentValidation extension methods for United Kingdom payment identifiers.
/// </summary>
public static class BritishPaymentValidatorExtensions
{
    /// <summary>
    /// Validates a UK bank sort code.
    /// </summary>
    /// <remarks>
    /// 6-digit code, typically formatted as XX-XX-XX. Dashes and spaces are stripped.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> SortCode<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SortCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:UkSortCode");
}
