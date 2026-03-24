using Microsoft.Extensions.Localization;

namespace Granit.Localization;

/// <summary>
/// Represents a string that can be localized at runtime via <see cref="IStringLocalizerFactory"/>.
/// Used by permission definitions to support i18n display names.
/// </summary>
public abstract class LocalizableString
{
    /// <summary>Resolves the display value using the given localizer factory.</summary>
    public abstract string Localize(IStringLocalizerFactory? localizerFactory);

    /// <summary>Creates a localizable string resolved from a resource type and key.</summary>
    public static LocalizableString Create<TResource>(string key) =>
        new LocalizedString(typeof(TResource), key);

    /// <summary>Creates a fixed (non-localizable) string.</summary>
    public static LocalizableString Fixed(string value) =>
        new FixedLocalizableString(value);
}

/// <summary>
/// A localizable string resolved from a resource type and localization key.
/// </summary>
internal sealed class LocalizedString(Type resourceType, string key) : LocalizableString
{
    /// <inheritdoc />
    public override string Localize(IStringLocalizerFactory? localizerFactory)
    {
        if (localizerFactory is null)
        {
            return key;
        }

        return localizerFactory.Create(resourceType)[key].Value;
    }
}

/// <summary>
/// A fixed string that is returned as-is regardless of culture.
/// </summary>
internal sealed class FixedLocalizableString(string value) : LocalizableString
{
    /// <inheritdoc />
    public override string Localize(IStringLocalizerFactory? localizerFactory) => value;
}
