namespace Granit.Privacy.DataExport.Exceptions;

/// <summary>
/// Transient failure raised by <c>PrivacyExportAssemblyService</c> when the sharded
/// archive assembly cannot complete but a retry has a chance of succeeding —
/// typically a blob-storage HTTP failure, multipart upload timeout, or transient
/// I/O error.
/// </summary>
/// <remarks>
/// <para>
/// Wolverine's retry-with-cooldown policy in
/// <c>Granit.Privacy.BackgroundJobs.Wolverine</c> matches this exception and
/// schedules retries at 1 min / 5 min / 15 min before moving the message to the
/// dead-letter queue. Non-transient failures (forged HMAC, malformed event,
/// programmer error) MUST NOT be wrapped in this exception — they should bubble up
/// as their original type so Wolverine's default error path moves them straight to
/// the DLQ without burning retry budget.
/// </para>
/// <para>
/// <b>Crash-resume.</b> The mid-flight per-shard checkpoint store ensures that a
/// retry picks up after the last committed shard rather than restarting from
/// fragment 0 — so even multi-hour assembly runs survive a transient failure
/// without re-doing successful work.
/// </para>
/// </remarks>
public sealed class PrivacyExportAssemblyException : Exception
{
    /// <summary>Saga / export-request correlation id of the failing assembly run.</summary>
    public Guid RequestId { get; }

    public PrivacyExportAssemblyException(Guid requestId, string message, Exception innerException)
        : base(message, innerException)
    {
        RequestId = requestId;
    }

    public PrivacyExportAssemblyException(Guid requestId, string message)
        : base(message)
    {
        RequestId = requestId;
    }
}
