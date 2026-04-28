using System.Globalization;
using FluentValidation.Resources;

namespace Granit.Validation.Internal;

/// <summary>
/// FluentValidation language manager that returns structured error codes
/// instead of human-readable messages.
/// </summary>
/// <remarks>
/// All built-in validator messages (e.g. <c>NotEmptyValidator</c>) are replaced
/// by codes following the convention <c>Granit:Validation:{ValidatorName}</c>.
/// The SPA resolves codes to localized strings using the dictionary served by
/// <c>GET /api/{version}/localization</c>, which supports per-tenant overrides.
/// <para>
/// Registered globally at startup via <c>AddGranitValidation()</c>:
/// <c>ValidatorOptions.Global.LanguageManager = new GranitErrorCodeLanguageManager()</c>.
/// </para>
/// </remarks>
internal sealed class GranitErrorCodeLanguageManager : ILanguageManager
{
    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    /// <remarks>
    /// Set to <see cref="CultureInfo.InvariantCulture"/> because codes are culture-agnostic:
    /// the language manager always returns an error code regardless of the requested culture.
    /// </remarks>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    public string GetString(string key, CultureInfo? culture = null) =>
        $"Granit:Validation:{key}";
}
