using System.Text.RegularExpressions;
using FluentValidation;
using Granit.Validation.Formats;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for common string format validators.
/// </summary>
public static partial class FormatValidatorExtensions
{
    // Hex color: # followed by 3, 4, 6, or 8 hex characters.
    // 3 = RGB shorthand, 4 = RGBA shorthand, 6 = RGB, 8 = RGBA.
    [GeneratedRegex(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.None, 100)]
    private static partial Regex ColorHexRegex();

    /// <summary>
    /// Validates a URL-friendly slug.
    /// </summary>
    /// <remarks>
    /// A slug may only contain lowercase letters, digits, and hyphens.
    /// Hyphens may not appear at the start, end, or consecutively.
    /// Examples: <c>my-blog-post</c>, <c>tenant-42</c>, <c>product</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Slug<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(SlugFormat.IsValid)
            .WithErrorCodeAndMessage("Validation:Format:Slug");

    /// <summary>
    /// Validates a Base64-encoded string.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Convert.TryFromBase64String"/> for validation.
    /// Accepts standard Base64 with padding. Rejects null, empty, and malformed strings.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Base64String<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value =>
                value?.Length > 0
                && Convert.TryFromBase64String(value, new byte[value.Length], out _))
            .WithErrorCodeAndMessage("Validation:Format:Base64String");

    /// <summary>
    /// Validates a CSS hex color code.
    /// </summary>
    /// <remarks>
    /// Accepts <c>#</c> followed by 3, 4, 6, or 8 hexadecimal characters:
    /// <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, or <c>#RRGGBBAA</c>.
    /// Examples: <c>#FF5733</c>, <c>#fff</c>, <c>#00FF00FF</c>.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> ColorHex<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && ColorHexRegex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Validation:Format:ColorHex");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidSlug(string? value) => SlugFormat.IsValid(value);

    internal static bool IsValidBase64String(string? value) =>
        value?.Length > 0 && Convert.TryFromBase64String(value, new byte[value.Length], out _);

    internal static bool IsValidColorHex(string? value) =>
        value is not null && ColorHexRegex().IsMatch(value.Trim());
}
