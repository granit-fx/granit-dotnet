using System.Reflection;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Granit.Http.ODataExposure.Internal;

/// <summary>
/// Builds an <see cref="IEdmModel"/> from the registered
/// <see cref="ODataEntitySetDescriptor"/>s. Per ADR-050, the EDM property set
/// is **explicitly whitelisted** from the entity's <c>ExportDefinition</c>
/// (resolved via the <c>EntityDefinition</c> orchestrator); convention-based
/// reflection on every public CLR property is rejected because it leaks
/// <see cref="Granit.Events.IDomainEvent"/> / <see cref="Granit.Events.IIntegrationEvent"/>
/// collections from <c>AggregateRoot</c> implementors into <c>$metadata</c>.
/// </summary>
internal static class ODataEdmModelBuilder
{
    /// <summary>Default OData container name for the tenant-feed mount.</summary>
    public const string TenantContainerName = "Container";

    /// <summary>OData container name for the host-feed mount. Distinct from <see cref="TenantContainerName"/> so a BI client cannot reuse one feed's <c>$metadata</c> document on the other URL by accident — schema mismatch surfaces immediately.</summary>
    public const string HostContainerName = "HostContainer";

    /// <summary>
    /// Builds the EDM model for the supplied descriptors, applying the
    /// per-type property whitelist resolved from each type's
    /// <c>ExportDefinition</c> at startup. Every type in
    /// <paramref name="whitelistByType"/> that is not an EntitySet root is a
    /// navigation-target type reachable through a whitelisted <c>$expand</c>
    /// path — it is registered explicitly via <c>AddEntityType</c> so the
    /// convention pass cannot pull it in with every public property exposed
    /// (#3005 — before this, navigation targets bypassed the ADR-050
    /// whitelist entirely). Properties absent from a type's whitelist
    /// (including framework-internal collections such as <c>DomainEvents</c> /
    /// <c>IntegrationEvents</c>) are removed before the convention pass runs.
    /// </summary>
    /// <param name="descriptors">EntitySet descriptors registered via the fluent options.</param>
    /// <param name="whitelistByType">Per-CLR-type whitelist covering every EntitySet root and every <c>$expand</c> closure target. Resolved by <c>ValidateAndResolveWhitelists</c> at startup.</param>
    /// <param name="containerName">OData container name; defaults to <see cref="TenantContainerName"/>. Host-feed callers pass <see cref="HostContainerName"/>.</param>
    /// <returns>The built <see cref="IEdmModel"/>, ready to wire into <c>WithODataModel</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="descriptors"/> is empty.</exception>
    public static IEdmModel Build(
        IReadOnlyList<ODataEntitySetDescriptor> descriptors,
        IReadOnlyDictionary<Type, ODataEntityTypeWhitelist> whitelistByType,
        string containerName = TenantContainerName)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(whitelistByType);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        if (descriptors.Count == 0)
        {
            throw new ArgumentException(
                "At least one EntitySet must be registered before building the EDM model.",
                nameof(descriptors));
        }

        ODataConventionModelBuilder builder = new() { ContainerName = containerName };
        Dictionary<Type, EntityTypeConfiguration> typeConfigurations = [];

        foreach (ODataEntitySetDescriptor descriptor in descriptors)
        {
            // AddEntitySet wires both the EntityType and the EntitySet
            // declaration; the convention model builder picks up keys from
            // properties named "Id" / "{Type}Id" during GetEdmModel.
            EntityTypeConfiguration entityTypeConfig = builder.AddEntityType(descriptor.EntityType);
            builder.AddEntitySet(descriptor.EntitySetName, entityTypeConfig);
            typeConfigurations.TryAdd(descriptor.EntityType, entityTypeConfig);
        }

        // $expand closure targets: registered explicitly (no EntitySet) so
        // the whitelist below applies to them exactly as to the roots.
        foreach (Type type in whitelistByType.Keys.Where(t => !typeConfigurations.ContainsKey(t)))
        {
            typeConfigurations[type] = builder.AddEntityType(type);
        }

        foreach ((Type type, EntityTypeConfiguration configuration) in typeConfigurations)
        {
            ApplyPropertyWhitelist(configuration, type, whitelistByType[type]);
        }

        return builder.GetEdmModel();
    }

    /// <summary>
    /// Calls <see cref="StructuralTypeConfiguration.RemoveProperty(PropertyInfo)"/>
    /// for every public CLR property of the entity type that is not (a) the
    /// type's <c>Id</c> key, (b) a scalar property listed in the
    /// export-derived whitelist, or (c) a navigation property listed in the
    /// type's resolved navigation whitelist. <c>RemoveProperty</c> adds to
    /// the type configuration's removed-properties list, which the
    /// <see cref="ODataConventionModelBuilder"/> consults during discovery —
    /// removed properties never make it into the EDM model, and notably never
    /// into <c>$metadata</c>.
    /// </summary>
    private static void ApplyPropertyWhitelist(
        EntityTypeConfiguration entityTypeConfig,
        Type entityType,
        ODataEntityTypeWhitelist whitelist)
    {
        HashSet<string> allowedScalars = new(whitelist.AllowedScalars, StringComparer.Ordinal);
        HashSet<string> allowedNavs = new(whitelist.AllowedNavigations, StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo property in entityType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            // The Id key stays — the convention builder identifies it by name.
            if (string.Equals(property.Name, "Id", StringComparison.Ordinal))
            {
                continue;
            }

            bool isNavigationLike = IsNavigationOrCollection(property.PropertyType);
            bool allowed = isNavigationLike
                ? allowedNavs.Contains(property.Name)
                : allowedScalars.Contains(property.Name);

            if (!allowed)
            {
                entityTypeConfig.RemoveProperty(property);
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the property type is a
    /// non-primitive non-string class or a collection type — these are the
    /// "navigation-like" shapes that the convention builder maps to
    /// <see cref="IEdmNavigationProperty"/> or treats as complex types.
    /// Strings, primitives, enums, value types, <see cref="Guid"/>,
    /// <see cref="DateTime"/>/<see cref="DateTimeOffset"/> and friends are
    /// scalar. Shared with the startup closure validator (#3005) so "is this
    /// segment a navigation?" means the same thing at validation time and at
    /// model-build time.
    /// </summary>
    internal static bool IsNavigationOrCollection(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        Type effective = Nullable.GetUnderlyingType(type) ?? type;

        if (effective.IsPrimitive || effective.IsEnum || effective == typeof(Guid)
            || effective == typeof(DateTime) || effective == typeof(DateTimeOffset)
            || effective == typeof(TimeSpan) || effective == typeof(decimal)
            || effective == typeof(DateOnly) || effective == typeof(TimeOnly))
        {
            return false;
        }

        // Anything else (IEnumerable<T>, custom class, IReadOnlyCollection<T>, …)
        // is a navigation-like shape from the EDM's perspective.
        return true;
    }

    /// <summary>
    /// Resolves the entity type a navigation property points at: the element
    /// type for collection navigations (<c>IEnumerable&lt;T&gt;</c> shapes,
    /// arrays), the property type itself for reference navigations. Used by
    /// the startup closure walk (#3005) to follow whitelisted <c>$expand</c>
    /// paths across CLR types.
    /// </summary>
    internal static Type ResolveNavigationTargetType(Type propertyType)
    {
        if (propertyType.IsArray)
        {
            return propertyType.GetElementType()!;
        }

        Type? enumerable = propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? propertyType
            : Array.Find(propertyType.GetInterfaces(), i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerable?.GetGenericArguments()[0] ?? propertyType;
    }
}
