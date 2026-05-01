namespace Granit.Entities.Actions;

/// <summary>
/// Cross-module hook for grafting an action onto an entity owned by another
/// module. Same IoC contributor pattern as <see cref="Relations.IEntityRelationContributor"/>
/// (per ADR-040) — used so that, for example, <c>Granit.Tasks</c> can graft an
/// <c>"add-task"</c> quick-action onto <c>Granit.Parties.Party</c> without either
/// side taking a runtime dependency on the other.
/// </summary>
public interface IEntityActionContributor
{
    /// <summary>
    /// Apply this module's action contributions. The runtime resolves them
    /// after every <see cref="EntityDefinition{TEntity}"/> is registered, so
    /// contributions targeting an unknown source entity are dropped with a
    /// debug log (module load order is non-deterministic, can't be strict).
    /// </summary>
    void Contribute(IEntityActionContributionContext context);
}

/// <summary>
/// Surface a contributor uses to graft actions onto a source entity.
/// </summary>
public interface IEntityActionContributionContext
{
    /// <summary>
    /// Adds an action to the source entity. The action appears alongside the
    /// source entity's intra-module actions in the manifest, ordered by
    /// <see cref="EntityActionBuilder{TEntity}.Order"/>.
    /// </summary>
    /// <param name="name">Stable action name, unique per source entity.</param>
    /// <param name="configure">Builder configuration delegate.</param>
    IEntityActionContributionContext AddAction<TSource>(
        string name,
        Action<EntityActionBuilder<TSource>> configure)
        where TSource : class;
}
