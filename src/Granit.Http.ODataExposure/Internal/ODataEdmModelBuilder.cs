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
    /// Builds the EDM model for the supplied descriptors, applying a per-entity
    /// scalar-property whitelist resolved from each entity's
    /// <c>ExportDefinition</c>. Properties absent from the whitelist (including
    /// framework-internal collections such as <c>DomainEvents</c> /
    /// <c>IntegrationEvents</c>) are <see cref="EntityTypeConfiguration.Ignore(PropertyInfo)"/>-ed
    /// before the convention pass runs. Navigation properties listed in the
    /// descriptor's <see cref="ODataEntitySetDescriptor.ExpandWhitelist"/>
    /// remain available to the consumer; un-whitelisted navigation properties
    /// are ignored too.
    /// </summary>
    /// <param name="descriptors">EntitySet descriptors registered via the fluent options.</param>
    /// <param name="scalarWhitelistByEntity">Scalar property names allowed on the EDM EntityType, per entity CLR type. Resolved from <c>ExportDefinition.GetFields()</c> at startup.</param>
    /// <param name="containerName">OData container name; defaults to <see cref="TenantContainerName"/>. Host-feed callers pass <see cref="HostContainerName"/>.</param>
    /// <returns>The built <see cref="IEdmModel"/>, ready to wire into <c>WithODataModel</c>.</returns>
    /// <exception cref="ArgumentException"><paramref name="descriptors"/> is empty.</exception>
    public static IEdmModel Build(
        IReadOnlyList<ODataEntitySetDescriptor> descriptors,
        IReadOnlyDictionary<Type, IReadOnlyList<string>> scalarWhitelistByEntity,
        string containerName = TenantContainerName)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(scalarWhitelistByEntity);
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName);

        if (descriptors.Count == 0)
        {
            throw new ArgumentException(
                "At least one EntitySet must be registered before building the EDM model.",
                nameof(descriptors));
        }

        ODataConventionModelBuilder builder = new() { ContainerName = containerName };

        foreach (ODataEntitySetDescriptor descriptor in descriptors)
        {
            // AddEntitySet wires both the EntityType and the EntitySet
            // declaration; the convention model builder picks up keys from
            // properties named "Id" / "{Type}Id" during GetEdmModel.
            EntityTypeConfiguration entityTypeConfig = builder.AddEntityType(descriptor.EntityType);
            builder.AddEntitySet(descriptor.EntitySetName, entityTypeConfig);

            ApplyPropertyWhitelist(
                entityTypeConfig,
                descriptor,
                scalarWhitelistByEntity[descriptor.EntityType]);
        }

        return builder.GetEdmModel();
    }

    /// <summary>
    /// Calls <see cref="StructuralTypeConfiguration.RemoveProperty(PropertyInfo)"/>
    /// for every public CLR property of the entity that is not (a) the entity's
    /// <c>Id</c> key, (b) a scalar property listed in the export-derived
    /// whitelist, or (c) a navigation property listed in the descriptor's
    /// <see cref="ODataEntitySetDescriptor.ExpandWhitelist"/>. <c>RemoveProperty</c>
    /// adds to the type configuration's removed-properties list, which the
    /// <see cref="ODataConventionModelBuilder"/> consults during discovery —
    /// ignored properties never make it into the EDM model, and notably never
    /// into <c>$metadata</c>.
    /// </summary>
    private static void ApplyPropertyWhitelist(
        EntityTypeConfiguration entityTypeConfig,
        ODataEntitySetDescriptor descriptor,
        IReadOnlyList<string> allowedScalarProperties)
    {
        HashSet<string> allowedScalars = new(allowedScalarProperties, StringComparer.Ordinal);
        HashSet<string> allowedNavs = new(
            descriptor.ExpandWhitelist ?? [],
            StringComparer.OrdinalIgnoreCase);

        foreach (PropertyInfo property in descriptor.EntityType.GetProperties(
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
    /// scalar.
    /// </summary>
    private static bool IsNavigationOrCollection(Type type)
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
}
