// ---------------------------------------------------------------------------
// JsonStringLocalizerFactory.cs
// Implements IStringLocalizerFactory to create JsonStringLocalizer instances.
// Thread-safe cache via ConcurrentDictionary<Type, Lazy<IStringLocalizer>>.
// Resolves resource inheritance, registered JSON sources, and optional DB overrides.
// ---------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using Granit.Localization;
using Granit.Localization.Options;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Granit.Localization.Internal;

/// <summary>
/// JSON localizer factory based on resources registered in
/// <see cref="GranitLocalizationOptions"/>.
/// </summary>
internal sealed class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Type, Lazy<IStringLocalizer>> _cache = new();
    private readonly IOptions<GranitLocalizationOptions> _options;
    private readonly ILocalizationOverrideStoreReader? _overrideStore;

    /// <summary>
    /// Creates a new factory. <paramref name="overrideStore"/> is optional:
    /// when <c>null</c>, DB overrides are not applied (transparent fallback to JSON only).
    /// </summary>
    public JsonStringLocalizerFactory(
        IOptions<GranitLocalizationOptions> options,
        ILocalizationOverrideStoreReader? overrideStore = null)
    {
        _options = options;
        _overrideStore = overrideStore;

        if (options.Value.EnableAutoDiscovery)
        {
            LocalizationAutoDiscovery.Discover(options.Value);
        }
    }

    /// <inheritdoc />
    public IStringLocalizer Create(Type resourceSource)
    {
        Lazy<IStringLocalizer> lazy = _cache.GetOrAdd(
            resourceSource,
            type => new Lazy<IStringLocalizer>(() => CreateLocalizer(type)));

        return lazy.Value;
    }

    /// <inheritdoc />
    public IStringLocalizer Create(string baseName, string location)
    {
        // Resolve by [LocalizationResourceName] attribute name (e.g. "Granit", "Features").
        // This is the primary resolution path used by GranitExceptionHandler and the SPA endpoint.
        Type? matchingType = _options.Value.Resources.GetAll()
            .Select(resourceInfo => resourceInfo.ResourceType)
            .FirstOrDefault(t => string.Equals(
                t.GetCustomAttribute<LocalizationResourceNameAttribute>()?.Name,
                baseName,
                StringComparison.Ordinal));

        if (matchingType is not null)
        {
            return Create(matchingType);
        }

        // Fallback: resolve by fully-qualified CLR type name (e.g. "Granit.Localization.GranitLocalizationResource, Granit.Localization").
        var resourceType = Type.GetType($"{baseName}, {location}");
        if (resourceType is not null)
        {
            return Create(resourceType);
        }

        // No matching resource — return empty localizer (key = returned value).
        return new JsonStringLocalizer([], "fr", []);
    }

    /// <summary>
    /// Creates a localizer for the given resource type, with inheritance.
    /// </summary>
    private JsonStringLocalizer CreateLocalizer(Type resourceType)
    {
        GranitLocalizationOptions options = _options.Value;

        if (!options.Resources.TryGetValue(resourceType, out LocalizationResourceInfo? info))
        {
            // Unregistered type: return an empty localizer
            return new JsonStringLocalizer([], "fr", []);
        }

        // Build localizers for parent resources (recursive)
        List<IStringLocalizer> baseLocalizers = [];
        foreach (Type baseType in info.BaseTypes)
        {
            baseLocalizers.Add(Create(baseType));
        }

        // Check [InheritResource] attributes on the marker class
        var inheritAttributes =
            (InheritResourceAttribute[])resourceType
                .GetCustomAttributes(typeof(InheritResourceAttribute), true);

        foreach (InheritResourceAttribute attr in inheritAttributes)
        {
            foreach (Type baseResourceType in attr.BaseResourceTypes.Where(t => !info.BaseTypes.Contains(t)))
            {
                baseLocalizers.Add(Create(baseResourceType));
            }
        }

        string resourceName = resourceType
            .GetCustomAttribute<LocalizationResourceNameAttribute>()?.Name
            ?? resourceType.Name;

        return new JsonStringLocalizer(
            info.JsonSources, info.DefaultCulture, baseLocalizers, resourceName, _overrideStore);
    }
}
