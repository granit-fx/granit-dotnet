using Granit.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications.MobilePush.Internal;

/// <summary>
/// No-op <see cref="IMobilePushEventPublisher"/> that logs a warning.
/// Replaced by Wolverine-backed implementation when <c>Granit.Notifications.Wolverine</c> is installed.
/// </summary>
internal sealed partial class NullMobilePushEventPublisher(
    ILogger<NullMobilePushEventPublisher> logger) : IMobilePushEventPublisher
{
    /// <inheritdoc/>
    public Task PublishTokenInvalidatedAsync(
        MobilePushTokenInvalidated tokenInvalidated,
        CancellationToken cancellationToken = default)
    {
        LogTokenInvalidationIgnored(LogRedaction.Token(tokenInvalidated.DeviceToken));
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "MobilePush token invalidation event for token '{RedactedToken}' was not published — " +
                  "no event publisher configured. Install Granit.Notifications.Wolverine for durable event dispatch.")]
    private partial void LogTokenInvalidationIgnored(string redactedToken);
}
