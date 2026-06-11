using FluentValidation;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for geographic coordinates.
/// </summary>
public static class GeoValidatorExtensions
{
    /// <summary>
    /// Validates that a latitude value is within the valid range [-90, 90].
    /// </summary>
    public static IRuleBuilderOptions<T, double> GeoLatitude<T>(this IRuleBuilder<T, double> ruleBuilder) =>
        ruleBuilder
            .Must(value => value is >= -90.0 and <= 90.0)
            .WithErrorCodeAndMessage("Validation:Format:GeoLatitude");

    /// <summary>
    /// Validates that a nullable latitude value is within the valid range [-90, 90].
    /// </summary>
    public static IRuleBuilderOptions<T, double?> GeoLatitude<T>(this IRuleBuilder<T, double?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value is null or (>= -90.0 and <= 90.0))
            .WithErrorCodeAndMessage("Validation:Format:GeoLatitude");

    /// <summary>
    /// Validates that a decimal latitude value is within the valid range [-90, 90].
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> GeoLatitude<T>(this IRuleBuilder<T, decimal> ruleBuilder) =>
        ruleBuilder
            .Must(value => value is >= -90m and <= 90m)
            .WithErrorCodeAndMessage("Validation:Format:GeoLatitude");

    /// <summary>
    /// Validates that a longitude value is within the valid range [-180, 180].
    /// </summary>
    public static IRuleBuilderOptions<T, double> GeoLongitude<T>(this IRuleBuilder<T, double> ruleBuilder) =>
        ruleBuilder
            .Must(value => value is >= -180.0 and <= 180.0)
            .WithErrorCodeAndMessage("Validation:Format:GeoLongitude");

    /// <summary>
    /// Validates that a nullable longitude value is within the valid range [-180, 180].
    /// </summary>
    public static IRuleBuilderOptions<T, double?> GeoLongitude<T>(this IRuleBuilder<T, double?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value is null or (>= -180.0 and <= 180.0))
            .WithErrorCodeAndMessage("Validation:Format:GeoLongitude");

    /// <summary>
    /// Validates that a decimal longitude value is within the valid range [-180, 180].
    /// </summary>
    public static IRuleBuilderOptions<T, decimal> GeoLongitude<T>(this IRuleBuilder<T, decimal> ruleBuilder) =>
        ruleBuilder
            .Must(value => value is >= -180m and <= 180m)
            .WithErrorCodeAndMessage("Validation:Format:GeoLongitude");
}
