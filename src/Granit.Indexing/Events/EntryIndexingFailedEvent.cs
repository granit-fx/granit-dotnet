using Granit.Events;

namespace Granit.Indexing.Events;

/// <summary>
/// Raised after an attempt to index an entry failed (backend error, validation, …).
/// Local (<see cref="IDomainEvent"/>) bus.
/// </summary>
/// <typeparam name="TKey">Resource primary key.</typeparam>
/// <param name="Key">Resource key whose indexing failed.</param>
/// <param name="TenantId">Owning tenant.</param>
/// <param name="Backend">Backend name (e.g. <c>"ef_tsvector"</c>).</param>
/// <param name="Reason">Stable snake_case identifier of the failure cause.</param>
public sealed record EntryIndexingFailedEvent<TKey>(
    TKey Key,
    Guid? TenantId,
    string Backend,
    string Reason) : IDomainEvent;
