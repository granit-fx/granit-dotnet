using System.Reflection;

namespace Granit.Entities.Actions;

/// <summary>
/// Default <see cref="IEntityActionContributionContext"/> — accumulates per-source
/// contributions in memory; the <c>Granit.Entities</c> runtime composer later
/// merges them into the canonical <see cref="EntityDefinitionDescriptor.Actions"/>
/// list. Sealed: the surface is not extensible — contributors interact through
/// the interface.
/// </summary>
public sealed class EntityActionContributionContext : IEntityActionContributionContext
{
    private readonly Dictionary<Type, List<EntityActionDescriptor>> _bySource = [];

    /// <summary>The accumulated contributions, indexed by source CLR type.</summary>
    public IReadOnlyDictionary<Type, IReadOnlyList<EntityActionDescriptor>> Contributions =>
        _bySource.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<EntityActionDescriptor>)kv.Value);

    /// <inheritdoc />
    public IEntityActionContributionContext AddAction<TSource>(
        string name,
        Action<EntityActionBuilder<TSource>> configure)
        where TSource : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        Assembly contributorAssembly = configure.Method.DeclaringType?.Assembly
            ?? Assembly.GetCallingAssembly();
        string assemblyName = contributorAssembly.GetName().Name ?? "unknown";

        EntityActionBuilder<TSource> builder = new(name, assemblyName);
        configure(builder);

        if (!_bySource.TryGetValue(typeof(TSource), out List<EntityActionDescriptor>? bag))
        {
            bag = [];
            _bySource[typeof(TSource)] = bag;
        }
        bag.Add(builder.Build());

        return this;
    }
}
