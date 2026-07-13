using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.Sse.Internal;

/// <summary>
/// Notification channel that pushes notifications to connected SSE clients
/// via the user's active HTTP connections (all tabs/devices).
/// </summary>
/// <remarks>
/// With an <see cref="ISseBackplane"/> registered (e.g.
/// <c>Granit.Notifications.Sse.StackExchangeRedis</c>) the message reaches every replica;
/// without one, delivery is LOCAL-NODE ONLY — loudly warned once outside Development,
/// because silent partial delivery on a multi-replica topology is a production incident,
/// not a preference.
/// </remarks>
internal sealed partial class SseNotificationChannel(
    ISseConnectionManager connectionManager,
    ILogger<SseNotificationChannel> logger,
    ISseBackplane? backplane = null,
    IHostEnvironment? environment = null) : INotificationChannel
{
    private int _noBackplaneWarned;

    /// <inheritdoc/>
    public string Name => NotificationChannels.Sse;

    /// <inheritdoc/>
    public async Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
    {
        SseNotificationMessage message = new()
        {
            NotificationId = context.NotificationId,
            NotificationTypeName = context.NotificationTypeName,
            Severity = context.Severity,
            Data = context.Data,
            RelatedEntityType = context.RelatedEntity?.EntityType,
            RelatedEntityId = context.RelatedEntity?.EntityId,
            OccurredAt = context.OccurredAt,
        };

        if (backplane is not null)
        {
            await backplane.PublishAsync(context.RecipientUserId, message, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (environment?.IsDevelopment() != true && Interlocked.Exchange(ref _noBackplaneWarned, 1) == 0)
        {
            LogNoBackplane();
        }

        await connectionManager
            .SendToUserAsync(context.RecipientUserId, message, cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SSE channel is running WITHOUT a backplane: messages only reach connections on this node. On a multi-replica topology this is silent partial delivery — reference Granit.Notifications.Sse.StackExchangeRedis.")]
    private partial void LogNoBackplane();
}
