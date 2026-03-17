using System.Threading.Channels;
using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Messages;

namespace Granit.AuditLog.EntityFrameworkCore.Internal.Services;

/// <summary>
/// Publishes <see cref="AuditLogBatch"/> messages to an in-process channel
/// for asynchronous persistence by <see cref="AuditLogPersistenceWorker"/>.
/// </summary>
internal sealed class ChannelAuditLogPublisher(
    Channel<AuditLogBatch> channel) : IAuditLogEntryPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(AuditLogBatch batch, CancellationToken cancellationToken = default) =>
        await channel.Writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
}
