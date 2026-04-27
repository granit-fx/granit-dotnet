using Microsoft.Extensions.Localization;

namespace Granit.Payments.Endpoints.Internal;

/// <summary>
/// Resolves the display label for a payment method type via <see cref="IStringLocalizer"/>.
/// Falls back to a human-readable Title Case transformation of the method type
/// when no localization resource is available (useful for third-party providers
/// declaring methods that aren't in the framework's locale files).
/// </summary>
internal static class PaymentMethodLabelResolver
{
    /// <summary>Resolves the localized display label for a method type.</summary>
    /// <param name="localizer">Localizer providing <c>Payments.Methods.{methodType}</c> keys.</param>
    /// <param name="methodType">Method type identifier (e.g., <c>card</c>, <c>sepa_debit</c>).</param>
    /// <returns>Localized label, or Title Case fallback if the key is missing.</returns>
    public static string Resolve(IStringLocalizer localizer, string methodType)
    {
        LocalizedString localized = localizer["Payments.Methods." + methodType];
        return localized.ResourceNotFound
            ? ToTitleCase(methodType)
            : localized.Value;
    }

    /// <summary>Converts a snake_case or kebab-case identifier to Title Case.</summary>
    /// <example><c>sepa_debit</c> → <c>Sepa Debit</c>; <c>klarna_pay_later</c> → <c>Klarna Pay Later</c>.</example>
    private static string ToTitleCase(string snakeOrKebab) =>
        string.Join(' ', snakeOrKebab
            .Split(['_', '-'])
            .Select(word => word.Length == 0
                ? word
                : char.ToUpperInvariant(word[0]) + word[1..]));
}
