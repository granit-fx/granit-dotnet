using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Granit.Dashboards.Json;

/// <summary>
/// Helpers to extend <see cref="WidgetDefinition"/>'s polymorphic JSON contract from
/// downstream packages — the three presentation-only widgets (Markdown, Image, Text)
/// are registered via <see cref="System.Text.Json.Serialization.JsonDerivedTypeAttribute"/>
/// directly on the base record, but data-bound widget kinds shipped by domain modules
/// (<c>Granit.Analytics</c>, future <c>Granit.IoT.Dashboards</c>, ...) cannot back-reference
/// the abstractions package. Those modules call
/// <see cref="AddDerivedType{TWidget}(JsonSerializerOptions, string)"/> at registration time
/// to extend the polymorphism chain without inverting the dependency direction.
/// </summary>
public static class WidgetDefinitionPolymorphism
{
    /// <summary>
    /// Adds a <see cref="WidgetDefinition"/>-derived type to the polymorphic JSON contract
    /// on the supplied <paramref name="options"/>. The discriminator string travels on the
    /// wire; pick a stable, lowercase, kebab-friendly tag (e.g. <c>"kpi"</c>, <c>"chart"</c>,
    /// <c>"iot.gauge"</c>).
    /// </summary>
    /// <typeparam name="TWidget">A concrete <see cref="WidgetDefinition"/>-derived type.</typeparam>
    /// <param name="options">Target serializer options — typically the host's shared
    /// <c>JsonSerializerOptions</c>.</param>
    /// <param name="discriminator">Stable type tag emitted on the wire under the
    /// <c>"type"</c> property name.</param>
    public static JsonSerializerOptions AddDerivedType<TWidget>(
        this JsonSerializerOptions options,
        string discriminator)
        where TWidget : WidgetDefinition
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(discriminator);

        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver())
            .WithAddedModifier(typeInfo =>
            {
                if (typeInfo.Type != typeof(WidgetDefinition))
                {
                    return;
                }

                typeInfo.PolymorphismOptions ??= new JsonPolymorphismOptions
                {
                    TypeDiscriminatorPropertyName = "type",
                };

                typeInfo.PolymorphismOptions.DerivedTypes.Add(
                    new JsonDerivedType(typeof(TWidget), discriminator));
            });

        return options;
    }
}
