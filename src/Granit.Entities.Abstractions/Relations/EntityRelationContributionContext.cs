using System.Reflection;

namespace Granit.Entities.Relations;

/// <summary>
/// Default <see cref="IEntityRelationContributionContext"/> — accumulates
/// per-source contributions in memory; the <c>Granit.Entities</c> runtime
/// composer later merges them into the canonical
/// <see cref="EntityDefinitionDescriptor.Relations"/> list. Sealed: the
/// surface is not extensible — contributors interact through the interface.
/// </summary>
public sealed class EntityRelationContributionContext : IEntityRelationContributionContext
{
    private readonly Dictionary<Type, List<RelationDescriptor>> _bySource = [];

    /// <summary>The accumulated contributions, indexed by source CLR type.</summary>
    public IReadOnlyDictionary<Type, IReadOnlyList<RelationDescriptor>> Contributions =>
        _bySource.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<RelationDescriptor>)kv.Value);

    /// <inheritdoc />
    public IEntityRelationContributionContext AddRelation<TSource, TRelated>(
        string name,
        string targetEntityName,
        Action<RelationBuilder<TSource, TRelated>> configure)
        where TSource : class
        where TRelated : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetEntityName);
        ArgumentNullException.ThrowIfNull(configure);

        // Cross-module contributions don't have an entity-side navigation
        // collection — the join is resolved server-side at request time, so
        // ForeignKeyExpression stays null. Contributor assembly name is
        // captured so the manifest can attribute the graft for diagnostics.
        Assembly contributorAssembly = configure.Method.DeclaringType?.Assembly
            ?? Assembly.GetCallingAssembly();
        string assemblyName = contributorAssembly.GetName().Name ?? "unknown";

        RelationBuilder<TSource, TRelated> builder = new(
            name,
            RelationCardinality.Many,
            foreignKeyExpression: null,
            contributorAssemblyName: assemblyName);
        configure(builder);

        if (!_bySource.TryGetValue(typeof(TSource), out List<RelationDescriptor>? bag))
        {
            bag = [];
            _bySource[typeof(TSource)] = bag;
        }
        bag.Add(builder.Build(targetEntityName));

        return this;
    }
}
