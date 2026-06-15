namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Forward-only write-counter decorator. Tracks how many bytes the wrapped
/// stream has accepted without imposing a cap — used by <c>ShardingArchiveWriter</c>
/// to decide when to roll over to a new shard.
/// </summary>
/// <remarks>
/// <see cref="CountingStream"/> short-circuits with
/// <c>PrivacyExportSizeLimitExceededException</c> on the global cap; this sibling
/// only observes. Both flavours co-exist because the sharding writer's per-shard
/// counter is a routing signal, not a defensive limit.
/// </remarks>
internal sealed class WriteCountingStream(Stream inner, bool leaveOpen)
    : WriteOnlyStreamDecorator(inner, leaveOpen)
{
    public long BytesWritten { get; private set; }

    protected override void OnWrite(ReadOnlySpan<byte> data) => BytesWritten += data.Length;
}
