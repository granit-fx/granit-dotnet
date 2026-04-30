using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Relations;

/// <summary>
/// Fluent builder for one relation declared on a source entity.
/// </summary>
/// <typeparam name="TSource">The source entity type.</typeparam>
/// <typeparam name="TRelated">The related entity type.</typeparam>
public sealed class RelationBuilder<TSource, TRelated>
    where TSource : class
    where TRelated : class
{
    private readonly string _name;
    private readonly RelationCardinality _cardinality;
    private readonly string? _foreignKeyExpression;

    private RelationDisplay _display = RelationDisplay.Tab;
    private string? _displayKey;
    private string? _icon;
    private int _order;
    private string? _requiresPermission;
    private string? _queryDefinitionName;
    private RelationAggregateBuilder<TRelated>? _aggregates;
    private readonly string? _contributorAssemblyName;

    internal RelationBuilder(
        string name,
        RelationCardinality cardinality,
        string? foreignKeyExpression,
        string? contributorAssemblyName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        _cardinality = cardinality;
        _foreignKeyExpression = foreignKeyExpression;
        _contributorAssemblyName = contributorAssemblyName;
    }

    /// <summary>Picks the display mode for this relation (defaults to <see cref="RelationDisplay.Tab"/>).</summary>
    public RelationBuilder<TSource, TRelated> DisplayAs(RelationDisplay display)
    {
        _display = display;
        return this;
    }

    /// <summary>i18n key for the user-facing label.</summary>
    public RelationBuilder<TSource, TRelated> DisplayKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _displayKey = key;
        return this;
    }

    /// <summary>Icon override for the smart-button / tab header.</summary>
    public RelationBuilder<TSource, TRelated> Icon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        _icon = icon;
        return this;
    }

    /// <summary>Display order within the source entity's relation list.</summary>
    public RelationBuilder<TSource, TRelated> Order(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>Permission gate — drops the relation from the manifest + aggregates response when the user does not hold it.</summary>
    public RelationBuilder<TSource, TRelated> RequiresPermission(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        _requiresPermission = permission;
        return this;
    }

    /// <summary>References a named <c>QueryDefinition</c> on the target entity for the drilldown collection.</summary>
    public RelationBuilder<TSource, TRelated> NavigateTo(string queryDefinitionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryDefinitionName);
        _queryDefinitionName = queryDefinitionName;
        return this;
    }

    /// <summary>Configures aggregates surfaced on the source entity's detail header.</summary>
    public RelationBuilder<TSource, TRelated> Aggregate(Action<RelationAggregateBuilder<TRelated>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _aggregates ??= new RelationAggregateBuilder<TRelated>();
        configure(_aggregates);
        return this;
    }

    internal RelationDescriptor Build(string targetEntityName) =>
        new(
            _name,
            _cardinality,
            _display,
            targetEntityName,
            typeof(TRelated),
            _displayKey,
            _icon,
            _order,
            _requiresPermission,
            _foreignKeyExpression,
            _aggregates?.Build() ?? [],
            _queryDefinitionName,
            _contributorAssemblyName);

    internal static string ResolveTargetEntityName(string? overrideName) =>
        overrideName ?? typeof(TRelated).FullName ?? typeof(TRelated).Name;

    /// <summary>
    /// Validates that <paramref name="collectionSelector"/> is a direct property
    /// access on the source entity and infers a stable foreign-key expression
    /// string for the descriptor.
    /// </summary>
    internal static (string PropertyName, string ForeignKeyExpression) ParseCollectionSelector(
        Expression<Func<TSource, IEnumerable<TRelated>>> collectionSelector)
    {
        ArgumentNullException.ThrowIfNull(collectionSelector);

        if (collectionSelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "Collection selector must be a direct property access expression (e.g. x => x.Addresses).",
                nameof(collectionSelector));
        }

        return (property.Name, $"{typeof(TSource).Name}.{property.Name}");
    }

    /// <summary>Same as <see cref="ParseCollectionSelector"/> but for 1:1 selectors.</summary>
    internal static (string PropertyName, string ForeignKeyExpression) ParseSingleSelector(
        Expression<Func<TSource, TRelated?>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        if (selector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "Selector must be a direct property access expression (e.g. x => x.PrimaryAddress).",
                nameof(selector));
        }

        return (property.Name, $"{typeof(TSource).Name}.{property.Name}");
    }
}
