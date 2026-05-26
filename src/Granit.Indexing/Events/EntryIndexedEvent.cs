using Granit.Events;

namespace Granit.Indexing.Events;

/// <summary>
/// Raised after an entry was successfully written to a backend. Local
/// (<see cref="IDomainEvent"/>) bus — never routed through the Wolverine outbox: the
/// downstream Elasticsearch sync handler (I-F2.2) lives in the same process.
/// </summary>
/// <typeparam name="TKey">Resource primary key.</typeparam>
/// <param name="Key">Resource key that was just indexed.</param>
/// <param name="TenantId">Owning tenant.</param>
/// <param name="Backend">Backend name (e.g. <c>"ef_tsvector"</c>) used for the write.</param>
public sealed record EntryIndexedEvent<TKey>(
    TKey Key,
    Guid? TenantId,
    string Backend) : IDomainEvent;
