namespace Granit.Entities.Customization.Domain;

/// <summary>
/// The five layout kinds covered by Layer 1 customization (ADR-053). Each
/// (tenant, entity, layout kind) tuple has at most one persisted
/// <see cref="EntityCustomization"/>; the deltas of one kind never affect
/// another.
/// </summary>
public enum LayoutKind
{
    /// <summary>The default form layout (<c>EntityDefinition.Form()</c>).</summary>
    FormDefault = 0,

    /// <summary>The default detail layout (<c>EntityDefinition.Detail()</c>).</summary>
    DetailDefault = 1,

    /// <summary>The list layout backed by the entity's <c>QueryDefinition</c>.</summary>
    List = 2,

    /// <summary>A calendar layout declared via <c>EntityDefinition.CalendarView(...)</c>.</summary>
    Calendar = 3,

    /// <summary>A gallery layout declared via <c>EntityDefinition.GalleryView(...)</c>.</summary>
    Gallery = 4,
}
