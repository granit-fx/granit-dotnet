namespace Granit.Timeline.Endpoints.Dtos;

/// <summary>
/// Request DTO for POST /timeline/&#123;entityType&#125;/&#123;entityId&#125;/anchor.
/// Materializes (or returns the existing) shadow row for an external entry,
/// so reactions and threaded replies can target a stable native id.
/// </summary>
public sealed record AnchorTimelineEntryRequest
{
    /// <summary>Contributor key declared by a registered <c>ITimelineSource</c> (e.g. <c>"auditing"</c>).</summary>
    public required string SourceKey { get; init; }

    /// <summary>External primary key in the contributor's store (string-encoded).</summary>
    public required string SourceId { get; init; }
}

/// <summary>Response for the anchor endpoint — the native entry id that subsequent reactions/replies must target.</summary>
/// <param name="EntryId">Deterministic v5 GUID derived from the anchor tuple.</param>
public sealed record AnchorTimelineEntryResponse(Guid EntryId);
