using System.Text.RegularExpressions;
using FluentValidation;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for communication identifiers (email, phone, URL).
/// </summary>
public static partial class ContactValidatorExtensions
{
    // Practical RFC 5321 subset: local-part chars + @ + domain labels + dot + TLD (2+ letters).
    // Rejects null, empty, missing @, spaces, and missing TLD.
    // Domain uses (label.)+ pattern to avoid backtracking (dot not in character class).
    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}$", RegexOptions.None, 100)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$", RegexOptions.None, 100)]
    private static partial Regex E164Regex();

    /// <summary>
    /// Validates an e-mail address against a practical RFC 5321 subset.
    /// </summary>
    /// <remarks>
    /// Accepts the common <c>local@domain.tld</c> format.
    /// Rejects null, empty strings, addresses without a domain, or those containing spaces.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Email<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && EmailRegex().IsMatch(value))
            .WithErrorCodeAndMessage("Validation:Format:Email");

    /// <summary>
    /// Validates a phone number in E.164 international format.
    /// </summary>
    /// <remarks>
    /// Expected format: <c>+</c> followed by 7 to 15 digits (e.g. <c>+32475123456</c>).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> E164Phone<T>(this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && E164Regex().IsMatch(value))
            .WithErrorCodeAndMessage("Validation:Format:E164Phone");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidEmail(string? value) =>
        value is not null && EmailRegex().IsMatch(value);

    internal static bool IsValidE164Phone(string? value) =>
        value is not null && E164Regex().IsMatch(value);
}
