using Granit.Domain.ValueObjects;
using Granit.Events;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published by the archive-assembly background job once every shard plus the signed
/// manifest sidecar are persisted. Distinct from <see cref="ExportCompletedEto"/> —
/// that event fires when the saga finishes gathering fragments, well before the
/// shards exist in storage. Notification handlers (and any downstream consumer that
/// needs to link to the actual archive bytes) MUST subscribe to this event, not the
/// saga's completion event, otherwise the user receives a download link before the
/// shards are uploaded.
/// </summary>
/// <param name="RequestId">Correlation id of the originating export request.</param>
/// <param name="UserId">Data subject whose archive is now downloadable.</param>
/// <param name="ManifestBlobReferenceId">Blob reference of the signed manifest
/// sidecar (the addressable artifact on the tracker).</param>
/// <param name="ShardCount">Number of shard ZIPs the assembler produced — drives the
/// per-shard link list rendered in the notification email.</param>
/// <param name="IsPartial"><c>true</c> when the saga reported a partial completion;
/// passed through so notification handlers can preserve the partial-vs-complete
/// distinction without re-reading the tracker.</param>
/// <param name="Regulation">Privacy regulation code the request was filed under.</param>
/// <param name="RequestedAt">When the data subject filed the request.</param>
/// <param name="TenantId">Tenant scope propagated from the originating request.</param>
public sealed record ExportArchiveAssembledEto(
    Guid RequestId,
    Guid UserId,
    BlobReference ManifestBlobReferenceId,
    int ShardCount,
    bool IsPartial,
    string Regulation,
    DateTimeOffset RequestedAt,
    Guid? TenantId = null) : IIntegrationEvent;
