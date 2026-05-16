// ---------------------------------------------------------------------------
// JsonStringLocalizerFactory.cs
// Implements IStringLocalizerFactory to create JsonStringLocalizer instances.
// Thread-safe cache via ConcurrentDictionary<Type, Lazy<IStringLocalizer>>.
// Resolves resource inheritance, registered JSON sources, and optional DB overrides.
// ---------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Reflection;
using Granit.Localization;
using Granit.Localization.Extensions;
using Granit.Localization.Options;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Granit.Localization.Internal;

/// <summary>
/// JSON localizer factory based on resources registered in
/// <see cref="GranitLocalizationOptions"/>.
/// </summary>
internal sealed partial class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly ConcurrentDictionary<Type, Lazy<IStringLocalizer>> _cache = new();
    private readonly IOptions<GranitLocalizationOptions> _options;
    private readonly ILocalizationOverrideStoreReader? _overrideStore;
    private readonly ILogger<JsonStringLocalizerFactory> _logger;

    /// <summary>
    /// Creates a new factory. <paramref name="overrideStore"/> is optional:
    /// when <c>null</c>, DB overrides are not applied (transparent fallback to JSON only).
    /// <paramref name="logger"/> is optional to keep backward compat for tests that instantiate
    /// the factory directly without a logger; <see cref="NullLogger{T}"/> is used as fallback.
    /// </summary>
    public JsonStringLocalizerFactory(
        IOptions<GranitLocalizationOptions> options,
        ILocalizationOverrideStoreReader? overrideStore = null,
        ILogger<JsonStringLocalizerFactory>? logger = null)
    {
        _options = options;
        _overrideStore = overrideStore;
        _logger = logger ?? NullLogger<JsonStringLocalizerFactory>.Instance;

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
            // Unregistered type: emit a diagnostic warning when the type is annotated as a
            // localization resource (so callers can see what to register), then fall back to
            // an empty localizer (preserves the minimal-host escape hatch).
            LocalizationResourceNameAttribute? attr =
                resourceType.GetCustomAttribute<LocalizationResourceNameAttribute>();

            if (attr is not null)
            {
                LogUnregisteredResource(
                    resourceType.FullName,
                    attr.Name,
                    resourceType.Assembly.GetName().Name,
                    resourceType.Name);
            }

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

    [LoggerMessage(
        LogLevel.Warning,
        "Localization resource {ResourceType} ({ResourceName}) requested but not registered. "
        + "Falling back on key suffix. Register it explicitly in assembly {Assembly}'s module "
        + "ConfigureServices via services.AddLocalizationResource<{ResourceTypeShort}>(), "
        + "or enable GranitLocalizationOptions.EnableAutoDiscovery=true.")]
    private partial void LogUnregisteredResource(
        string? resourceType,
        string resourceName,
        string? assembly,
        string resourceTypeShort);
}
