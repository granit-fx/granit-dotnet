using System.Threading.Channels;
using Granit.Auditing.Messages;

namespace Granit.Auditing.Internal.Services;

/// <summary>
/// Publishes <see cref="AuditingBatch"/> messages to an in-process channel
/// for asynchronous persistence by <see cref="AuditingPersistenceWorker"/>.
/// </summary>
internal sealed class ChannelAuditingPublisher(
    Channel<AuditingBatch> channel) : IAuditEntryPublisher
{
    /// <inheritdoc/>
    public async ValueTask PublishAsync(AuditingBatch batch, CancellationToken cancellationToken = default) =>
        await channel.Writer.WriteAsync(batch, cancellationToken).ConfigureAwait(false);
}
