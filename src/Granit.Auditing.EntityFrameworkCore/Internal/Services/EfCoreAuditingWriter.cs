using Granit.Auditing.Domain;

namespace Granit.Auditing.EntityFrameworkCore.Internal.Services;

/// <summary>
/// EF Core implementation of <see cref="IAuditingWriter"/> for persisting
/// explicit audit log entries (login events, access denied, config changes).
/// Delegates to the <see cref="AuditPersistencePipeline"/> so explicit writes emit the
/// same <c>AuditEntryPersistedEto</c> and metrics as interceptor-captured entries.
/// </summary>
internal sealed class EfCoreAuditingWriter(AuditPersistencePipeline pipeline) : IAuditingWriter
{
    /// <inheritdoc/>
    /// <remarks>The entry's <c>Id</c> is populated on return.</remarks>
    public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return pipeline.PersistAsync(entry, cancellationToken);
    }
}
