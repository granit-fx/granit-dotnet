using FluentValidation;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for pagination parameters (page number and page size).
/// </summary>
/// <remarks>
/// These extensions use built-in FluentValidation validators (<c>GreaterThanOrEqualTo</c>,
/// <c>InclusiveBetween</c>) whose error codes are already localized by
/// <see cref="GranitErrorCodeLanguageManager"/>. No additional localization keys are required.
/// </remarks>
public static class PaginationValidatorExtensions
{
    /// <summary>
    /// Default maximum page size, consistent with <c>QueryEngineDefaults.MaxPageSize</c>.
    /// </summary>
    /// <remarks>
    /// Defined locally to avoid coupling <c>Granit.Validation</c> to <c>Granit.QueryEngine</c>.
    /// </remarks>
    public const int DefaultMaxPageSize = 100;

    /// <summary>
    /// Validates that a page number is at least 1.
    /// </summary>
    public static IRuleBuilderOptions<T, int> ValidPage<T>(
        this IRuleBuilder<T, int> ruleBuilder) =>
        ruleBuilder.GreaterThanOrEqualTo(1);

    /// <summary>
    /// Validates that a page size is between 1 and <paramref name="maxPageSize"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(
        this IRuleBuilder<T, int> ruleBuilder, int maxPageSize = DefaultMaxPageSize) =>
        ruleBuilder.InclusiveBetween(1, maxPageSize);

    /// <summary>
    /// Validates that a nullable page number is at least 1 (when provided).
    /// </summary>
    public static IRuleBuilderOptions<T, int?> ValidPage<T>(
        this IRuleBuilder<T, int?> ruleBuilder) =>
        ruleBuilder.GreaterThanOrEqualTo(1);

    /// <summary>
    /// Validates that a nullable page size is between 1 and <paramref name="maxPageSize"/> (when provided).
    /// </summary>
    public static IRuleBuilderOptions<T, int?> ValidPageSize<T>(
        this IRuleBuilder<T, int?> ruleBuilder, int maxPageSize = DefaultMaxPageSize) =>
        ruleBuilder.InclusiveBetween(1, maxPageSize);
}
