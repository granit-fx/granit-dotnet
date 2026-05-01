using Granit.Entities.Forms;

namespace Granit.Entities.Layouts;

/// <summary>
/// Card-content schema for a kanban tile. Frappe-style three-slot layout:
/// title (large headline), then ordered body fields. The renderer caps the
/// number of body fields visually — the descriptor is descriptive, not
/// prescriptive, so per-card density is a theme decision.
/// </summary>
/// <remarks>
/// Fields reference entity properties that MUST also appear in the entity's
/// <c>QueryDefinition</c> column whitelist. Otherwise the projection cannot
/// emit the value and the card renders blank — pairing enforced by the
/// architecture tests.
/// </remarks>
public sealed record KanbanCardDescriptor
{
    /// <summary>
    /// Property used as the tile headline. <see langword="null"/> means the
    /// renderer falls back to the entity's <see cref="EntityDefinitionDescriptor.DisplayProperty"/>.
    /// </summary>
    public string? TitleProperty { get; init; }

    /// <summary>Body fields, in declaration order.</summary>
    public required IReadOnlyList<FieldDescriptor> Fields { get; init; }
}
