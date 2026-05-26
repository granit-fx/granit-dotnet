namespace Granit.Indexing.BackgroundJobs.Exceptions;

/// <summary>
/// Thrown when a <c>RebuildIndexJob</c> dispatch races a concurrent run for the same
/// <c>(TenantId, SourceName)</c> tuple and loses on the checkpoint store's optimistic
/// concurrency check.
/// </summary>
/// <remarks>
/// Wolverine's default retry/DLQ policy dead-letters the duplicate so it does not clobber
/// the winning run's checkpoint. Hosts that want to swallow the duplicate (idempotent
/// dispatch) can catch the exception in a Wolverine policy and discard the envelope.
/// </remarks>
public sealed class RebuildAlreadyInProgressException : Exception
{
    /// <summary>Stable identifier for the cause. Always <c>rebuild_already_in_progress</c>.</summary>
    public string Reason { get; } = "rebuild_already_in_progress";

    /// <summary>Tenant scope of the racing checkpoint write.</summary>
    public Guid? TenantId { get; }

    /// <summary>Source name of the racing checkpoint write.</summary>
    public string SourceName { get; }

    public RebuildAlreadyInProgressException(Guid? tenantId, string sourceName, Exception? innerException = null)
        : base(
            $"A rebuild is already in progress for source '{sourceName}' on tenant '{tenantId?.ToString() ?? "global"}'.",
            innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        TenantId = tenantId;
        SourceName = sourceName;
    }
}
