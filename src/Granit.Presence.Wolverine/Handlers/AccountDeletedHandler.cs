using Granit.Identity.Local.Events;
using Granit.Presence.Abstractions;
using Microsoft.Extensions.Logging;

namespace Granit.Presence.Wolverine.Handlers;

/// <summary>
/// Wolverine handler reacting to <see cref="AccountDeletedEto"/> by purging the
/// user's persistent presence override and live heartbeat (GDPR Art. 17).
/// </summary>
/// <remarks>
/// Idempotent: both <see cref="IPresenceStore.DeleteAsync"/> and
/// <see cref="IPresenceTracker.RemoveAsync"/> are no-ops when the row / cache entry
/// is already absent, so a retried delivery is safe.
/// </remarks>
public class AccountDeletedHandler
{
    public static async Task HandleAsync(
        AccountDeletedEto message,
        IPresenceStore store,
        IPresenceTracker tracker,
        ILogger<AccountDeletedHandler> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tracker);
        ArgumentNullException.ThrowIfNull(logger);

        await store.DeleteAsync(message.UserId, cancellationToken).ConfigureAwait(false);
        await tracker.RemoveAsync(message.UserId, cancellationToken).ConfigureAwait(false);
        AccountDeletedHandlerLog.PresencePurgedAfterAccountDeleted(logger, message.UserId);
    }
}

internal static partial class AccountDeletedHandlerLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Purged presence data for deleted user {UserId}.")]
    public static partial void PresencePurgedAfterAccountDeleted(ILogger logger, Guid userId);
}
