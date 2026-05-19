// ---------------------------------------------------------------------------
// JsonStringLocalizer.cs
// Implements IStringLocalizer with resolution from JSON dictionaries.
// Supports: native culture fallback (CultureInfo.Parent), parent resource
// inheritance, {0} formatting, thread-safe cache via Lazy<T>, and optional
// DB override resolution via ILocalizationOverrideStoreReader (DB > JSON).
// ---------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using SmartFormat;
using SmartFormat.Extensions;

namespace Granit.Localization.Internal;

/// <summary>
/// Localizer based on embedded JSON dictionaries with culture fallback and inheritance.
/// DB overrides (when <see cref="ILocalizationOverrideStoreReader"/> is registered) take priority
/// over every embedded JSON file.
/// </summary>
/// <remarks>
/// Resolution order: DB override → JSON (culture chain) → JSON (default culture) → inheritance.
/// </remarks>
/// <param name="sources">Embedded JSON sources for this resource.</param>
/// <param name="defaultCulture">Default culture of the resource.</param>
/// <param name="baseLocalizers">Localizers of parent resources (inheritance).</param>
/// <param name="resourceName">Logical resource name used for DB override lookup. Null when not available.</param>
/// <param name="overrideStore">Optional DB override store. Null when not configured.</param>
internal sealed class JsonStringLocalizer(
    List<EmbeddedJsonSource> sources,
    string defaultCulture,
    List<IStringLocalizer> baseLocalizers,
    string? resourceName = null,
    ILocalizationOverrideStoreReader? overrideStore = null) : IStringLocalizer
{
    /// <summary>
    /// SmartFormatter without <see cref="ReflectionSource"/> to prevent template injection
    /// via DB override values (e.g., <c>{0.Password}</c> accessing argument properties).
    /// </summary>
    private static readonly SmartFormatter SafeFormatter = CreateSafeFormatter();

    private readonly ConcurrentDictionary<string, Lazy<Dictionary<string, string>>> _cultureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<EmbeddedJsonSource> _sources = sources;
    private readonly string _defaultCulture = defaultCulture;
    private readonly List<IStringLocalizer> _baseLocalizers = baseLocalizers;
    private readonly string? _resourceName = resourceName;
    private readonly ILocalizationOverrideStoreReader? _overrideStore = overrideStore;

    /// <inheritdoc />
    public LocalizedString this[string name]
    {
        get
        {
            string? value = GetTranslation(name, CultureInfo.CurrentUICulture);
            return value is not null
                ? new LocalizedString(name, value, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);
        }
    }

    /// <inheritdoc />
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            string? value = GetTranslation(name, CultureInfo.CurrentUICulture);

            if (value is null)
            {
                return new LocalizedString(name, name, resourceNotFound: true);
            }

            string formatted = arguments.Length == 0
                ? value
                : SafeFormatter.Format(CultureInfo.CurrentCulture, value, arguments);

            return new LocalizedString(name, formatted, resourceNotFound: false);
        }
    }

    /// <inheritdoc />
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        HashSet<string> seen = [];

        // 0. DB overrides for current culture — highest priority
        if (_overrideStore is not null && _resourceName is not null)
        {
            IReadOnlyDictionary<string, string> dbOverrides =
                GetOrLoadOverrides(CultureInfo.CurrentUICulture.Name);

            foreach (KeyValuePair<string, string> kvp in dbOverrides.Where(kvp => seen.Add(kvp.Key)))
            {
                yield return new LocalizedString(kvp.Key, kvp.Value, resourceNotFound: false);
            }
        }

        // Walk the culture chain (JSON)
        CultureInfo currentCulture = CultureInfo.CurrentUICulture;
        while (true)
        {
            foreach (KeyValuePair<string, string> kvp in GetOrLoadDictionary(currentCulture.Name).Where(kvp => seen.Add(kvp.Key)))
            {
                yield return new LocalizedString(kvp.Key, kvp.Value, resourceNotFound: false);
            }

            if (!includeParentCultures || currentCulture == CultureInfo.InvariantCulture)
            {
                break;
            }

            currentCulture = currentCulture.Parent;
        }

        // Fallback to the default culture
        if (includeParentCultures)
        {
            foreach (KeyValuePair<string, string> kvp in GetOrLoadDictionary(_defaultCulture).Where(kvp => seen.Add(kvp.Key)))
            {
                yield return new LocalizedString(kvp.Key, kvp.Value, resourceNotFound: false);
            }
        }

        // Inheritance: include keys from parent resources
        foreach (LocalizedString localizedString in _baseLocalizers
            .SelectMany(b => b.GetAllStrings(includeParentCultures))
            .Where(s => seen.Add(s.Name)))
        {
            yield return localizedString;
        }
    }

    /// <summary>
    /// Resolves a translation: DB override first, then JSON culture chain, then inheritance.
    /// </summary>
    private string? GetTranslation(string name, CultureInfo culture)
    {
        // 0. DB override — highest priority
        if (_overrideStore is not null && _resourceName is not null)
        {
            IReadOnlyDictionary<string, string> dbOverrides = GetOrLoadOverrides(culture.Name);
            if (dbOverrides.TryGetValue(name, out string? dbValue))
            {
                return dbValue;
            }
        }

        // 1. Walk up the culture chain via CultureInfo.Parent
        CultureInfo currentCulture = culture;
        while (currentCulture != CultureInfo.InvariantCulture)
        {
            Dictionary<string, string> dictionary = GetOrLoadDictionary(currentCulture.Name);
            if (dictionary.TryGetValue(name, out string? value))
            {
                return value;
            }

            currentCulture = currentCulture.Parent;
        }

        // 2. Fallback to the resource's default culture
        Dictionary<string, string> defaultDictionary = GetOrLoadDictionary(_defaultCulture);
        if (defaultDictionary.TryGetValue(name, out string? defaultValue))
        {
            return defaultValue;
        }

        // 3. Inheritance: search in parent resources
        foreach (IStringLocalizer baseLocalizer in _baseLocalizers)
        {
            LocalizedString result = baseLocalizer[name];
            if (!result.ResourceNotFound)
            {
                return result.Value;
            }
        }

        // 4. Key not found
        return null;
    }

    /// <summary>
    /// Returns DB overrides for the given culture by delegating to
    /// <see cref="CachedLocalizationOverrideStore"/> which handles tenant-scoped caching
    /// and invalidation via FusionCache.
    /// </summary>
    /// <remarks>
    /// The blocking <c>GetAwaiter().GetResult()</c> call is safe because
    /// <see cref="CachedLocalizationOverrideStore.GetOverridesAsync"/> returns synchronously
    /// from L1 memory cache on cache hits. No secondary cache is maintained here to avoid
    /// cross-tenant leakage and stale-cache issues.
    /// </remarks>
    private IReadOnlyDictionary<string, string> GetOrLoadOverrides(string cultureName) =>
        _overrideStore!.GetOverridesAsync(_resourceName!, cultureName).GetAwaiter().GetResult();

    /// <summary>
    /// Loads or retrieves from cache the dictionary for a given culture.
    /// Uses Lazy&lt;T&gt; to guarantee a single load even under high concurrency.
    /// </summary>
    private Dictionary<string, string> GetOrLoadDictionary(string cultureName)
    {
        Lazy<Dictionary<string, string>> lazy = _cultureCache.GetOrAdd(
            cultureName,
            name => new Lazy<Dictionary<string, string>>(() => LoadDictionary(name)));

        return lazy.Value;
    }

    /// <summary>
    /// Loads translations for a culture from all JSON sources.
    /// Sources added last take priority (application-level override).
    /// </summary>
    private Dictionary<string, string> LoadDictionary(string cultureName)
    {
        Dictionary<string, string> merged = new(StringComparer.Ordinal);

        foreach (EmbeddedJsonSource source in _sources)
        {
            Dictionary<string, Dictionary<string, string>> allCultures =
                JsonLocalizationDictionaryBuilder.Build(source.Assembly, source.ResourcePrefix);

            if (allCultures.TryGetValue(cultureName, out Dictionary<string, string>? texts))
            {
                foreach (KeyValuePair<string, string> kvp in texts)
                {
                    merged[kvp.Key] = kvp.Value;
                }
            }
        }

        return merged;
    }

    /// <summary>
    /// Creates a <see cref="SmartFormatter"/> without <see cref="ReflectionSource"/>
    /// to prevent template injection via DB override values.
    /// </summary>
    /// <remarks>
    /// Override values are admin-controlled (via <c>Localization.Overrides.Manage</c> permission).
    /// Without this restriction, a malicious override like <c>{0.Password}</c> could access
    /// properties of format arguments via SmartFormat's reflection-based member resolution.
    /// Removing <see cref="ReflectionSource"/> limits format strings to positional placeholders
    /// (<c>{0}</c>, <c>{1}</c>), named dictionary keys, and built-in formatters (plural, conditional).
    /// </remarks>
    private static SmartFormatter CreateSafeFormatter()
    {
        SmartFormatter formatter = Smart.CreateDefaultSmartFormat();
        formatter.RemoveSourceExtension<ReflectionSource>();
        return formatter;
    }
}
