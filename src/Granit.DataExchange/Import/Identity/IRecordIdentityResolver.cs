namespace Granit.DataExchange.Import.Identity;

/// <summary>
/// Resolves the persistence identity (insert/update/upsert/skip/ambiguous) of a batch of
/// imported entities in a single call — batched so implementations can issue one query per
/// chunk instead of one per row.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <remarks>
/// Three concrete strategies are provided in <c>Granit.DataExchange.EntityFrameworkCore</c>:
/// <list type="bullet">
///   <item><b>ExternalIdResolver</b>: uses a dedicated external ID mapping table for stable cross-system references.</item>
///   <item><b>BusinessKeyResolver</b>: uses a single business key property declared in <c>ImportDefinition&lt;T&gt;</c>.</item>
///   <item><b>CompositeKeyResolver</b>: uses multiple properties combined as a composite key.</item>
/// </list>
/// Implementations that need to detect duplicate keys within the same file should track state
/// (e.g. a <c>HashSet&lt;EntityKey&gt;</c>) across calls — the resolver instance is scoped to a
/// single import run, not re-created per batch.
/// </remarks>
public interface IRecordIdentityResolver<TEntity> where TEntity : class
{
    /// <summary>
    /// Resolves the identity of every entity in <paramref name="batch"/>.
    /// </summary>
    /// <param name="batch">The mapped entities in this chunk, in row order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// One <see cref="RecordIdentity"/> per input entity, in the same order
    /// (<c>result[i]</c> corresponds to <c>batch[i]</c>).
    /// </returns>
    Task<IReadOnlyList<RecordIdentity>> ResolveBatchAsync(
        IReadOnlyList<TEntity> batch,
        CancellationToken cancellationToken = default);
}
