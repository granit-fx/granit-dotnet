using System.Globalization;

namespace Granit.Domain;

/// <summary>
/// In-memory resolution extensions for <see cref="ITranslatable{TTranslation}"/> entities.
/// Operates on already-loaded translation collections (after EF Core <c>.Include()</c>).
/// </summary>
public static class TranslatableExtensions
{
    /// <summary>
    /// Resolves the best translation for the requested culture.
    /// </summary>
    /// <remarks>
    /// <para>When <paramref name="useFallback"/> is <c>true</c> (default), the resolution chain is:</para>
    /// <list type="number">
    ///   <item>Exact culture match (e.g. <c>"fr-BE"</c>)</item>
    ///   <item>Parent culture fallback (e.g. <c>"fr-BE"</c> → <c>"fr"</c>)</item>
    ///   <item>Default culture fallback (<paramref name="defaultCulture"/>)</item>
    ///   <item>First available translation</item>
    /// </list>
    /// <para>
    /// When <paramref name="useFallback"/> is <c>false</c> (strict / culture-variant mode),
    /// only the exact culture match is returned. Returns <c>null</c> if the culture does not exist.
    /// </para>
    /// </remarks>
    /// <typeparam name="TTranslation">The translation type.</typeparam>
    /// <param name="entity">The translatable entity with loaded translations.</param>
    /// <param name="culture">The desired BCP 47 culture (e.g. <c>"fr"</c>, <c>"fr-BE"</c>).</param>
    /// <param name="useFallback">
    /// <c>true</c> to use fallback resolution (default); <c>false</c> for strict culture match.
    /// </param>
    /// <param name="defaultCulture">Fallback culture when no match in the hierarchy. Defaults to <c>"en"</c>.</param>
    /// <returns>The best matching translation, or <c>null</c> if none found.</returns>
    public static TTranslation? GetTranslation<TTranslation>(
        this ITranslatable<TTranslation> entity,
        string culture,
        bool useFallback = true,
        string defaultCulture = "en")
        where TTranslation : class, ITranslation
    {
        ICollection<TTranslation> translations = entity.Translations;

        if (translations.Count == 0)
        {
            return null;
        }

        // 1. Exact match
        TTranslation? exact = FindByCulture(translations, culture);
        if (exact is not null)
        {
            return exact;
        }

        if (!useFallback)
        {
            return null;
        }

        // 2. Walk up the culture hierarchy (fr-BE → fr → invariant)
        var cultureInfo = CultureInfo.GetCultureInfo(culture);
        CultureInfo parent = cultureInfo.Parent;
        while (parent != CultureInfo.InvariantCulture)
        {
            TTranslation? parentMatch = FindByCulture(translations, parent.Name);
            if (parentMatch is not null)
            {
                return parentMatch;
            }

            parent = parent.Parent;
        }

        // 3. Default culture fallback
        if (!string.Equals(culture, defaultCulture, StringComparison.OrdinalIgnoreCase))
        {
            TTranslation? defaultMatch = FindByCulture(translations, defaultCulture);
            if (defaultMatch is not null)
            {
                return defaultMatch;
            }
        }

        // 4. Return first available (better than null for display)
        return translations.FirstOrDefault();
    }

    private static TTranslation? FindByCulture<TTranslation>(
        ICollection<TTranslation> translations,
        string culture)
        where TTranslation : class, ITranslation =>
        translations.FirstOrDefault(t =>
            string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
}
