using System.Linq.Expressions;
using System.Reflection;
using Granit.Entities.Details;
using Granit.Entities.Forms;
using Granit.Entities.Relations;

namespace Granit.Entities;

/// <summary>
/// Fluent builder for one entity's UI surface. Used inside
/// <see cref="EntityDefinition{TEntity}.Configure"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type the definition targets.</typeparam>
public sealed class EntityDefinitionBuilder<TEntity> where TEntity : class
{
    private string? _displayKey;
    private string? _icon;
    private string? _permissionGroup;
    private string? _displayProperty;

    private Type? _queryDefinitionType;
    private Type? _exportDefinitionType;
    private readonly List<Type> _metricDefinitionTypes = [];
    private readonly List<Type> _dashboardDefinitionTypes = [];
    private Type? _workflowDefinitionType;

    private readonly List<Func<FormDescriptor>> _formFactories = [];
    private readonly List<Func<DetailDescriptor>> _detailFactories = [];
    private readonly List<RelationDescriptor> _relations = [];

    /// <summary>Sets the i18n key for the entity's display name (singular).</summary>
    public EntityDefinitionBuilder<TEntity> DisplayKey(string displayKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayKey);
        _displayKey = displayKey;
        return this;
    }

    /// <summary>Sets the icon name (from the framework's icon catalog).</summary>
    public EntityDefinitionBuilder<TEntity> Icon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        _icon = icon;
        return this;
    }

    /// <summary>
    /// Sets the permission-group prefix used to infer <c>{Group}.{Resource}.{Action}</c>
    /// permissions for the manifest's <c>permissions.canList/Read/...</c> fields.
    /// </summary>
    public EntityDefinitionBuilder<TEntity> PermissionGroup(string permissionGroup)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionGroup);
        _permissionGroup = permissionGroup;
        return this;
    }

    /// <summary>
    /// Names the property used as the entity's display label in references
    /// (lambda must be a direct property access).
    /// </summary>
    public EntityDefinitionBuilder<TEntity> DisplayProperty<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "DisplayProperty selector must be a direct property access expression (e.g. x => x.Title).",
                nameof(propertySelector));
        }

        _displayProperty = property.Name;
        return this;
    }

    /// <summary>
    /// References the <c>QueryDefinition&lt;TEntity&gt;</c> implementation that backs the
    /// list collection of this entity. Resolved at boot time through DI by the integrity
    /// check; at request time the manifest aggregates the matching <c>QueryMetadata</c>
    /// into the entity's <c>list</c> facet.
    /// </summary>
    public EntityDefinitionBuilder<TEntity> Query<TQueryDefinition>()
        where TQueryDefinition : class
    {
        _queryDefinitionType = typeof(TQueryDefinition);
        return this;
    }

    /// <summary>References the <c>ExportDefinition&lt;TEntity&gt;</c> for the entity's CSV / XLSX export.</summary>
    public EntityDefinitionBuilder<TEntity> Export<TExportDefinition>()
        where TExportDefinition : class
    {
        _exportDefinitionType = typeof(TExportDefinition);
        return this;
    }

    /// <summary>References a <c>MetricDefinition&lt;TEntity, TValue&gt;</c> surfaced as a KPI on the entity's detail header.</summary>
    public EntityDefinitionBuilder<TEntity> Metric<TMetricDefinition>()
        where TMetricDefinition : class
    {
        _metricDefinitionTypes.Add(typeof(TMetricDefinition));
        return this;
    }

    /// <summary>References a <c>DashboardDefinition</c> embedded on the entity's detail header (collapsible by default).</summary>
    public EntityDefinitionBuilder<TEntity> Dashboard<TDashboardDefinition>()
        where TDashboardDefinition : class
    {
        _dashboardDefinitionTypes.Add(typeof(TDashboardDefinition));
        return this;
    }

    /// <summary>References the <c>IWorkflowDefinition&lt;TState&gt;</c> that gates this entity's state transitions (per ADR-046 integration).</summary>
    public EntityDefinitionBuilder<TEntity> Workflow<TWorkflowDefinition>()
        where TWorkflowDefinition : class
    {
        _workflowDefinitionType = typeof(TWorkflowDefinition);
        return this;
    }

    /// <summary>
    /// Adds a form variant. Variants are addressable from the front via
    /// <c>&lt;EntityForm name=... variant=... /&gt;</c>. Names must be unique per entity.
    /// </summary>
    public EntityDefinitionBuilder<TEntity> Form(string name, Action<FormBuilder<TEntity>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        FormBuilder<TEntity> builder = new(name);
        configure(builder);
        _formFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Adds a detail-view variant. Variants are addressable from the front via
    /// <c>&lt;EntityDetail name=... variant=... /&gt;</c>. Names must be unique per entity.
    /// </summary>
    public EntityDefinitionBuilder<TEntity> Detail(string name, Action<DetailBuilder<TEntity>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        DetailBuilder<TEntity> builder = new(name);
        configure(builder);
        _detailFactories.Add(builder.Build);
        return this;
    }

    /// <summary>
    /// Declares a 1:N relation from this entity to <typeparamref name="TRelated"/>
    /// via a navigation collection on the source CLR type. The
    /// <paramref name="collectionSelector"/> must be a direct property access
    /// expression (e.g. <c>p =&gt; p.Addresses</c>); otherwise the call throws.
    /// </summary>
    /// <typeparam name="TRelated">The related entity type.</typeparam>
    /// <param name="collectionSelector">Lambda pointing at the navigation collection on the source.</param>
    /// <param name="configure">Optional fluent configuration delegate.</param>
    public EntityDefinitionBuilder<TEntity> HasMany<TRelated>(
        Expression<Func<TEntity, IEnumerable<TRelated>>> collectionSelector,
        Action<RelationBuilder<TEntity, TRelated>>? configure = null)
        where TRelated : class
    {
        (string propertyName, string foreignKeyExpression) =
            RelationBuilder<TEntity, TRelated>.ParseCollectionSelector(collectionSelector);

        RelationBuilder<TEntity, TRelated> builder = new(propertyName, RelationCardinality.Many, foreignKeyExpression);
        configure?.Invoke(builder);

        string targetEntityName = RelationBuilder<TEntity, TRelated>.ResolveTargetEntityName(null);
        _relations.Add(builder.Build(targetEntityName));
        return this;
    }

    /// <summary>
    /// Declares a 1:1 relation from this entity to <typeparamref name="TRelated"/>
    /// via a navigation property on the source CLR type. The
    /// <paramref name="selector"/> must be a direct property access expression.
    /// </summary>
    public EntityDefinitionBuilder<TEntity> HasOne<TRelated>(
        Expression<Func<TEntity, TRelated?>> selector,
        Action<RelationBuilder<TEntity, TRelated>>? configure = null)
        where TRelated : class
    {
        (string propertyName, string foreignKeyExpression) =
            RelationBuilder<TEntity, TRelated>.ParseSingleSelector(selector);

        RelationBuilder<TEntity, TRelated> builder = new(propertyName, RelationCardinality.One, foreignKeyExpression);
        configure?.Invoke(builder);

        string targetEntityName = RelationBuilder<TEntity, TRelated>.ResolveTargetEntityName(null);
        _relations.Add(builder.Build(targetEntityName));
        return this;
    }

    internal EntityDefinitionDescriptor Build(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IReadOnlyList<FormDescriptor> forms = [.. _formFactories.Select(f => f())];
        IReadOnlyList<DetailDescriptor> details = [.. _detailFactories.Select(f => f())];

        AssertUniqueVariantNames(forms.Select(f => f.Name).ToList(), nameof(Form));
        AssertUniqueVariantNames(details.Select(d => d.Name).ToList(), nameof(Detail));
        AssertUniqueRelationNames(_relations);

        IReadOnlyList<RelationDescriptor> relations = [.. _relations
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Name, StringComparer.Ordinal)];

        return new EntityDefinitionDescriptor
        {
            Name = name,
            EntityType = typeof(TEntity),
            DisplayKey = _displayKey,
            Icon = _icon,
            PermissionGroup = _permissionGroup,
            DisplayProperty = _displayProperty,
            QueryDefinitionType = _queryDefinitionType,
            ExportDefinitionType = _exportDefinitionType,
            MetricDefinitionTypes = _metricDefinitionTypes.AsReadOnly(),
            DashboardDefinitionTypes = _dashboardDefinitionTypes.AsReadOnly(),
            WorkflowDefinitionType = _workflowDefinitionType,
            Forms = forms,
            Details = details,
            Relations = relations,
        };
    }

    private static void AssertUniqueRelationNames(IReadOnlyList<RelationDescriptor> relations)
    {
        IGrouping<string, RelationDescriptor>? duplicate = relations
            .GroupBy(r => r.Name, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate relation name '{duplicate.Key}' on entity '{typeof(TEntity).FullName}'. Names must be unique per entity.");
        }
    }

    private static void AssertUniqueVariantNames(IReadOnlyList<string> names, string variantKind)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (!seen.Add(name))
            {
                throw new InvalidOperationException(
                    $"Duplicate {variantKind} variant name '{name}' on entity '{typeof(TEntity).FullName}'. Names must be unique per entity.");
            }
        }
    }
}
