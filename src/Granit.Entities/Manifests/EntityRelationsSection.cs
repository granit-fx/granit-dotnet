using Granit.Entities.Relations;

namespace Granit.Entities.Manifests;

/// <summary>
/// One relation surfaced in the manifest's Relations facet.
/// </summary>
/// <param name="Name">Stable relation name, unique per source entity.</param>
/// <param name="Cardinality">1:N or 1:1.</param>
/// <param name="Display">Display mode (Tab / SmartButton / Sidebar / InlineChips).</param>
/// <param name="TargetEntityName">Wire identifier of the target <c>EntityDefinition</c>.</param>
/// <param name="DisplayKey">i18n key for the user-facing label.</param>
/// <param name="Icon">Icon override.</param>
/// <param name="Order">Display order within the source's relation list.</param>
/// <param name="QueryDefinitionName">Optional named QueryDefinition used for the drilldown collection.</param>
/// <param name="Aggregates">Aggregates surfaced by this relation (count / sum / avg / min / max).</param>
/// <param name="ContributorAssemblyName">Assembly that contributed this relation. Null for intra-module declarations; populated for cross-module grafts.</param>
public sealed record EntityRelationManifest(
    string Name,
    RelationCardinality Cardinality,
    RelationDisplay Display,
    string TargetEntityName,
    string? DisplayKey,
    string? Icon,
    int Order,
    string? QueryDefinitionName,
    IReadOnlyList<EntityRelationAggregateManifest> Aggregates,
    string? ContributorAssemblyName);

/// <summary>One aggregate on a relation.</summary>
/// <param name="Kind">Aggregate kind.</param>
/// <param name="PropertyName">Property name on the related entity (or <see langword="null"/> for Count).</param>
/// <param name="LabelKey">i18n key for the user-facing label.</param>
/// <param name="Format">Optional formatter hint (e.g. <c>"currency"</c>).</param>
public sealed record EntityRelationAggregateManifest(
    RelationAggregateKind Kind,
    string? PropertyName,
    string? LabelKey,
    string? Format);
