using System.Security.Claims;

namespace Granit.Entities.Relations;

/// <summary>
/// Computes aggregate values for a set of relations on a single source row.
/// The framework ships <c>NullRelationAggregateService</c> as the default —
/// hosts that want real values register an EF Core (or other) implementation
/// over it.
/// </summary>
/// <remarks>
/// <para>
/// Implementations SHOULD execute each relation aggregate in parallel
/// (typically <c>Task.WhenAll</c>) — that is the explicit acceptance
/// criterion of story #1561. Implementations SHOULD also emit one
/// OpenTelemetry activity per relation so the per-aggregate latency is
/// visible without sampling.
/// </para>
/// <para>
/// Aggregates that the runner cannot compute for a relation (unknown
/// property, type mismatch, …) MUST return a <see cref="RelationAggregateValue"/>
/// with the ill-defined slot left <see langword="null"/> rather than throwing —
/// the calling endpoint surfaces a partial result so one bad relation does
/// not blank-out the whole detail page.
/// </para>
/// </remarks>
public interface IRelationAggregateService
{
    /// <summary>
    /// Computes <see cref="RelationAggregateValue"/> for each
    /// <paramref name="relationNames"/> on the source identified by
    /// (<paramref name="sourceEntityName"/>, <paramref name="sourceId"/>).
    /// </summary>
    /// <param name="sourceEntityName">Wire identifier of the source entity (e.g. <c>"Granit.Parties.Party"</c>).</param>
    /// <param name="sourceId">String form of the source primary key — caller does not assume Guid.</param>
    /// <param name="relationNames">The relations whose aggregates the caller wants.</param>
    /// <param name="user">The requesting principal — implementations MAY use this to bound queries by tenant / row-level permissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per relation in <paramref name="relationNames"/>; relations the runner could not resolve are absent.</returns>
    Task<IReadOnlyDictionary<string, RelationAggregateValue>> ComputeAsync(
        string sourceEntityName,
        string sourceId,
        IReadOnlyList<string> relationNames,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation — returns an empty dictionary. Hosts that don't
/// want real aggregate computation can leave this in place; the manifest's
/// <see cref="RelationDescriptor.Aggregates"/> still surfaces the wire shape,
/// the endpoint just returns no values.
/// </summary>
public sealed class NullRelationAggregateService : IRelationAggregateService
{
    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, RelationAggregateValue>> ComputeAsync(
        string sourceEntityName,
        string sourceId,
        IReadOnlyList<string> relationNames,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, RelationAggregateValue> empty =
            new Dictionary<string, RelationAggregateValue>(StringComparer.Ordinal);
        return Task.FromResult(empty);
    }
}
