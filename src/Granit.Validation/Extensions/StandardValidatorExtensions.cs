using System.Text.RegularExpressions;
using FluentValidation;
using Granit.Validation.Internal;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for ISO standard codes and identifiers.
/// </summary>
public static partial class StandardValidatorExtensions
{
    // ISO 8601 duration: P[nY][nM][nD][T[nH][nM][nS]]
    // At least one component is required after P. Fractional seconds supported.
    [GeneratedRegex(
        @"^P(?=\d|T\d)(\d+Y)?(\d+M)?(\d+D)?(T(?=\d)(\d+H)?(\d+M)?(\d+(\.\d+)?S)?)?$",
        RegexOptions.None, 100)]
    private static partial Regex Iso8601DurationRegex();

    // UUID/GUID: 8-4-4-4-12 hexadecimal digits (RFC 9562).
    [GeneratedRegex(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase, 100)]
    private static partial Regex UuidRegex();

    /// <summary>
    /// Validates an ISO 4217 currency code (e.g. <c>EUR</c>, <c>USD</c>).
    /// </summary>
    /// <remarks>
    /// Validates both the format (3 uppercase letters) and existence in the official
    /// ISO 4217 active currency list. Case-insensitive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Iso4217CurrencyCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(Iso4217CurrencyCodeAlgorithm.IsValid)
            .WithErrorCodeAndMessage("Granit:Validation:InvalidIso4217CurrencyCode");

    /// <summary>
    /// Validates an ISO 8601 duration (e.g. <c>P1Y2M3DT4H5M6S</c>, <c>PT30M</c>).
    /// </summary>
    /// <remarks>
    /// Supports year, month, day, hour, minute, and second components.
    /// Fractional seconds are allowed (e.g. <c>PT1.5S</c>).
    /// At least one component must be present after <c>P</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Iso8601Duration<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && Iso8601DurationRegex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Granit:Validation:InvalidIso8601Duration");

    /// <summary>
    /// Validates a UUID/GUID in canonical format per RFC 9562.
    /// </summary>
    /// <remarks>
    /// Accepts the standard 8-4-4-4-12 hexadecimal format
    /// (e.g. <c>550e8400-e29b-41d4-a716-446655440000</c>). Case-insensitive.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Uuid<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && UuidRegex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Granit:Validation:InvalidUuid");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidIso8601Duration(string? value) =>
        value is not null && Iso8601DurationRegex().IsMatch(value.Trim());

    internal static bool IsValidUuid(string? value) =>
        value is not null && UuidRegex().IsMatch(value.Trim());
}
