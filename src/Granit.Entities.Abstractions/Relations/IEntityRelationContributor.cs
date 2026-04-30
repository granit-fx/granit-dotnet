namespace Granit.Entities.Relations;

/// <summary>
/// Cross-module hook for grafting a relation onto an entity owned by another
/// module. Third application of the IoC contributor pattern (per ADR-040, after
/// <c>IWorkspaceContributor</c> and the upcoming <c>IActivityTypeProvider</c>).
/// </summary>
/// <remarks>
/// Used so that, for example, <c>Granit.Invoicing</c> can graft an
/// <c>"invoices"</c> smart-button relation onto <c>Granit.Parties.Party</c>
/// without either side taking a runtime dependency on the other —
/// <c>Granit.Invoicing</c> only references <c>Granit.Parties.Abstractions</c>
/// for the CLR type and <c>Granit.Entities.Abstractions</c> for this surface.
/// </remarks>
public interface IEntityRelationContributor
{
    /// <summary>
    /// Apply this module's relation contributions. The runtime resolves them
    /// after every <see cref="EntityDefinition{TEntity}"/> is registered, so
    /// contributions targeting an unknown source entity are dropped with a
    /// debug log (module load order is non-deterministic, can't be strict).
    /// </summary>
    void Contribute(IEntityRelationContributionContext context);
}

/// <summary>
/// Surface a contributor uses to graft relations onto a source entity.
/// </summary>
public interface IEntityRelationContributionContext
{
    /// <summary>
    /// Adds a 1:N relation from <typeparamref name="TSource"/> to
    /// <typeparamref name="TRelated"/>. The relation appears alongside the
    /// source entity's intra-module relations in the manifest, ordered by
    /// <see cref="RelationBuilder{TSource,TRelated}.Order"/>.
    /// </summary>
    /// <param name="name">Stable relation name, unique per source entity.</param>
    /// <param name="targetEntityName">
    /// Wire identifier of the target <c>EntityDefinition</c>. Required for
    /// cross-module grafts because the contributor cannot rely on type-name
    /// inference when the target entity's <c>Name</c> diverges from its CLR full name.
    /// </param>
    /// <param name="configure">Builder configuration delegate.</param>
    IEntityRelationContributionContext AddRelation<TSource, TRelated>(
        string name,
        string targetEntityName,
        Action<RelationBuilder<TSource, TRelated>> configure)
        where TSource : class
        where TRelated : class;
}

/// <summary>
/// Surface a relation contribution exposes to the runtime — the source entity
/// CLR type plus the bag of <see cref="RelationDescriptor"/>s to merge in.
/// </summary>
public interface IEntityRelationContribution
{
    /// <summary>CLR type of the source entity.</summary>
    Type SourceEntityType { get; }

    /// <summary>Relations to merge into the source entity's compiled descriptor.</summary>
    IReadOnlyList<RelationDescriptor> Relations { get; }
}
