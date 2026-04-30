using Granit.Entities.Details;
using Granit.Entities.Forms;
using Granit.Entities.Relations;

namespace Granit.Entities;

/// <summary>
/// Immutable descriptor of one entity's UI surface, built by an
/// <see cref="EntityDefinition{TEntity}"/> via the fluent
/// <see cref="EntityDefinitionBuilder{TEntity}"/>.
/// </summary>
/// <remarks>
/// <para>
/// References to other declarative primitives (<c>QueryDefinition</c>,
/// <c>ExportDefinition</c>, <c>MetricDefinition</c>, <c>DashboardDefinition</c>,
/// <c>IWorkflowDefinition</c>) are stored as <see cref="Type"/> values; the
/// <c>Granit.Entities</c> runtime resolves each one through DI at boot time
/// (integrity check, story #1541) and at request time (manifest aggregation,
/// story #1548).
/// </para>
/// </remarks>
public sealed record EntityDefinitionDescriptor
{
    /// <summary>Wire identifier (e.g. <c>"Granit.Parties.Party"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>The entity's CLR type.</summary>
    public required Type EntityType { get; init; }

    /// <summary>i18n key for the user-facing display name (singular form).</summary>
    public string? DisplayKey { get; init; }

    /// <summary>Optional icon name from the icon catalog (e.g. <c>"users"</c>, <c>"file-text"</c>).</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Permission-group prefix for inferring <c>{Group}.{Resource}.Read</c>-style
    /// permissions. When set, the manifest's <c>permissions.canList/Read/...</c>
    /// fields are computed from this prefix at request time.
    /// </summary>
    public string? PermissionGroup { get; init; }

    /// <summary>
    /// Property name (PascalCase) used as the entity's display label in references
    /// (e.g. <c>"Number"</c> for an Invoice → "Invoice INV-001"). Optional.
    /// </summary>
    public string? DisplayProperty { get; init; }

    /// <summary>
    /// CLR type of the referenced <c>QueryDefinition&lt;TEntity&gt;</c>, or <see langword="null"/>
    /// when no list collection is exposed.
    /// </summary>
    public Type? QueryDefinitionType { get; init; }

    /// <summary>
    /// CLR type of the referenced <c>ExportDefinition&lt;TEntity&gt;</c>, or <see langword="null"/>
    /// when no export is exposed.
    /// </summary>
    public Type? ExportDefinitionType { get; init; }

    /// <summary>
    /// CLR types of the referenced <c>MetricDefinition&lt;TEntity, TValue&gt;</c> instances.
    /// Empty when no metrics are surfaced.
    /// </summary>
    public required IReadOnlyList<Type> MetricDefinitionTypes { get; init; }

    /// <summary>
    /// CLR types of the referenced <c>DashboardDefinition</c> instances surfaced on
    /// this entity's detail header. Empty when no dashboards are surfaced.
    /// </summary>
    public required IReadOnlyList<Type> DashboardDefinitionTypes { get; init; }

    /// <summary>
    /// CLR type of the referenced <c>IWorkflowDefinition&lt;TState&gt;</c>, or <see langword="null"/>
    /// when no workflow gates this entity.
    /// </summary>
    public Type? WorkflowDefinitionType { get; init; }

    /// <summary>Form variants declared via <c>b.Form("name", ...)</c>. Empty when no form is exposed.</summary>
    public required IReadOnlyList<FormDescriptor> Forms { get; init; }

    /// <summary>Detail variants declared via <c>b.Detail("name", ...)</c>. Empty when no detail view is exposed.</summary>
    public required IReadOnlyList<DetailDescriptor> Details { get; init; }

    /// <summary>
    /// Relations declared on this entity — both intra-module via
    /// <c>HasMany&lt;T&gt;</c> / <c>HasOne&lt;T&gt;</c> on the builder and
    /// cross-module via <see cref="IEntityRelationContributor"/> grafts.
    /// Sorted by <see cref="RelationDescriptor.Order"/> then
    /// <see cref="RelationDescriptor.Name"/>; intra-module declarations take
    /// precedence on conflicts (same <see cref="RelationDescriptor.Name"/>).
    /// </summary>
    public IReadOnlyList<RelationDescriptor> Relations { get; init; } = [];
}
