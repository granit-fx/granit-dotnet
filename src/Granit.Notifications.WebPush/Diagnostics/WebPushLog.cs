using Microsoft.Extensions.Logging;

namespace Granit.Notifications.WebPush.Diagnostics;

/// <summary>
/// High-performance structured log messages for the Web Push channel.
/// </summary>
internal static partial class WebPushLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit.Notifications.WebPush is using the in-memory subscription store — browser "
            + "push subscriptions will not persist across restarts and are NOT shared across "
            + "replicas (a subscription saved on one pod is invisible to the others, so pushes "
            + "dispatched elsewhere silently reach nobody). Register "
            + "Granit.Notifications.WebPush.EntityFrameworkCore for durable, multi-replica storage.")]
    public static partial void InMemoryStoreActiveInNonDevelopment(ILogger logger);
}
