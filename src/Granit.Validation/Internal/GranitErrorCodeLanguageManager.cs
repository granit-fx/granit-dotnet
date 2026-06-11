using System.Globalization;
using FluentValidation;
using FluentValidation.Resources;
using Microsoft.Extensions.Localization;

namespace Granit.Validation.Internal;

/// <summary>
/// FluentValidation language manager that resolves built-in validator error codes
/// to fully localized, interpolated messages in the current request culture.
/// </summary>
/// <remarks>
/// <para>
/// FluentValidation calls <see cref="GetString"/> with a bare validator name
/// (e.g. <c>MinimumLengthValidator</c>) to obtain the <em>message template</em>, then
/// interpolates the placeholders it tracked during validation
/// (<c>{PropertyName}</c>, <c>{MinLength}</c>, <c>{TotalLength}</c>, …) via its
/// <c>MessageFormatter</c>. This manager returns the localized template for the
/// <c>Validation:{validatorName}</c> key from the embedded <c>Validation</c> resource,
/// so the final <c>errors</c> map carries real sentences rather than bare codes.
/// </para>
/// <para>
/// The localizer is wired post-build by
/// <c>GranitValidationModule.OnApplicationInitialization</c> (the global instance is set
/// on <c>ValidatorOptions.Global.LanguageManager</c>). Until then — and for any key absent
/// from the resource — resolution degrades gracefully to the bare <c>Validation:{key}</c>
/// code, preserving the previous behavior for hosts without localization wired and for
/// unit tests that instantiate validators directly.
/// </para>
/// <para>
/// Culture follows the ambient <see cref="CultureInfo.CurrentUICulture"/> (set by the
/// ASP.NET request localization middleware from <c>Accept-Language</c>), since the
/// underlying <see cref="IStringLocalizer"/> resolves against it.
/// </para>
/// </remarks>
internal sealed class GranitErrorCodeLanguageManager : ILanguageManager
{
    private const string KeyPrefix = "Validation:";
    private const string BuiltinPrefix = "Validation:Builtin:";
    private const string ValidatorSuffix = "Validator";

    /// <summary>
    /// Localizer for the <c>Validation</c> resource. <c>null</c> until wired at
    /// application initialization; resolution falls back to the bare code while unset.
    /// </summary>
    public IStringLocalizer? Localizer { get; set; }

    /// <inheritdoc/>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc/>
    /// <remarks>
    /// Unused: the actual culture comes from <see cref="CultureInfo.CurrentUICulture"/>
    /// via <see cref="Localizer"/>. Kept on <see cref="CultureInfo.InvariantCulture"/>
    /// to satisfy the interface contract.
    /// </remarks>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    /// <remarks>
    /// FluentValidation passes the bare validator name (e.g. <c>NotEmptyValidator</c>) as the
    /// key. Built-in validators (suffix <c>Validator</c>) map to <c>Validation:Builtin:{Rule}</c>
    /// per ADR-066 (the wire <c>ErrorCode</c> is unaffected — FluentValidation owns it).
    /// </remarks>
    public string GetString(string key, CultureInfo? culture = null) =>
        ResolveTemplate(
            key.EndsWith(ValidatorSuffix, StringComparison.Ordinal)
                ? BuiltinPrefix + key[..^ValidatorSuffix.Length]
                : KeyPrefix + key);

    /// <summary>
    /// Resolves a fully-prefixed <c>Validation:*</c> key to its localized template
    /// for the current culture, falling back to the key itself when the localizer is
    /// not wired or the key is absent from the resource.
    /// </summary>
    /// <param name="fullKey">The fully-qualified key (e.g. <c>Validation:MinimumLengthValidator</c>).</param>
    /// <returns>The localized template, or <paramref name="fullKey"/> when unresolved.</returns>
    public string ResolveTemplate(string fullKey)
    {
        IStringLocalizer? localizer = Localizer;
        if (localizer is null)
        {
            return fullKey;
        }

        LocalizedString localized = localizer[fullKey];
        return localized.ResourceNotFound ? fullKey : localized.Value;
    }

    /// <summary>
    /// Resolves a <c>Validation:*</c> key through the globally-registered
    /// <see cref="GranitErrorCodeLanguageManager"/>. Used by
    /// <c>WithErrorCodeAndMessage</c>, whose explicit <c>WithMessage</c> bypasses the
    /// normal language-manager path.
    /// </summary>
    /// <param name="fullKey">The fully-qualified <c>Validation:*</c> key.</param>
    /// <returns>The localized template, or <paramref name="fullKey"/> when unresolved.</returns>
    public static string ResolveMessage(string fullKey) =>
        ValidatorOptions.Global.LanguageManager is GranitErrorCodeLanguageManager manager
            ? manager.ResolveTemplate(fullKey)
            : fullKey;
}
