namespace Granit.Entities.Relations;

/// <summary>
/// Immutable descriptor for one relation declared on an entity — covers both
/// intra-module declarations (<c>HasMany&lt;T&gt;</c> / <c>HasOne&lt;T&gt;</c> on
/// <see cref="EntityDefinitionBuilder{TEntity}"/>) and cross-module grafts
/// (<see cref="IEntityRelationContributor"/>).
/// </summary>
/// <param name="Name">Stable relation name, unique per source entity (e.g. <c>"invoices"</c>).</param>
/// <param name="Cardinality">1:N or 1:1.</param>
/// <param name="Display">How the renderer should surface this relation (Tab / SmartButton / Sidebar / InlineChips).</param>
/// <param name="TargetEntityName">Wire identifier of the target <c>EntityDefinition</c> (e.g. <c>"Granit.Invoicing.Invoice"</c>) — resolved at boot via the registry.</param>
/// <param name="TargetEntityClrType">CLR type of the related entity. Available for reflection-based consumers; the wire form sticks to the registry name.</param>
/// <param name="DisplayKey">i18n key for the user-facing label (e.g. <c>"Relation:Party.Invoices"</c>).</param>
/// <param name="Icon">Icon override on the smart-button / tab header. Falls back to the target entity's icon when <see langword="null"/>.</param>
/// <param name="Order">Display order within the source entity's relation list.</param>
/// <param name="RequiresPermission">Optional permission gate — drops the relation from the manifest payload + aggregates response when the user does not hold it (defense in depth, story #1562).</param>
/// <param name="ForeignKeyExpression">String form of the lambda <c>related =&gt; related.SourceId == source.Id</c> used by intra-module declarations. Cross-module contributions leave this <see langword="null"/> and rely on a server-side join hook resolved at request time.</param>
/// <param name="Aggregates">Aggregates surfaced by this relation, in declaration order.</param>
/// <param name="QueryDefinitionName">Optional reference to a <c>QueryDefinition</c> on the target entity — when set, the renderer uses it for the drilldown collection instead of the target entity's default query.</param>
/// <param name="ContributorAssemblyName">Name of the assembly that contributed this relation. <see langword="null"/> for intra-module declarations; populated by the contribution context for cross-module grafts.</param>
public sealed record RelationDescriptor(
    string Name,
    RelationCardinality Cardinality,
    RelationDisplay Display,
    string TargetEntityName,
    Type TargetEntityClrType,
    string? DisplayKey,
    string? Icon,
    int Order,
    string? RequiresPermission,
    string? ForeignKeyExpression,
    IReadOnlyList<RelationAggregateDescriptor> Aggregates,
    string? QueryDefinitionName,
    string? ContributorAssemblyName);
