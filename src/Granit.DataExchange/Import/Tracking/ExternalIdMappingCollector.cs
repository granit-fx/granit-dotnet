namespace Granit.DataExchange.Import.Tracking;

/// <summary>
/// Scoped collector that accumulates external-to-internal ID mappings during import
/// execution. The orchestrator flushes pending entries to the persistence store
/// after the executor commits.
/// </summary>
internal sealed class ExternalIdMappingCollector
{
    private readonly List<PendingMapping> _pending = [];

    /// <summary>
    /// Records a new external-to-internal ID mapping for a successfully inserted entity.
    /// </summary>
    public void Track(string externalId, Guid internalId)
        => _pending.Add(new PendingMapping(externalId, internalId));

    /// <summary>Accumulated mappings waiting to be flushed.</summary>
    public IReadOnlyList<PendingMapping> Pending => _pending;

    /// <summary>Whether any mappings were tracked.</summary>
    public bool HasPending => _pending.Count > 0;

    internal sealed record PendingMapping(string ExternalId, Guid InternalId);
}
