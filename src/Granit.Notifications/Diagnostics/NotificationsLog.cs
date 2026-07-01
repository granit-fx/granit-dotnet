using Microsoft.Extensions.Logging;

namespace Granit.Notifications.Diagnostics;

/// <summary>
/// High-performance structured log messages for the notification engine.
/// </summary>
internal static partial class NotificationsLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit.Notifications is using in-memory stores — notifications, preferences and "
            + "subscriptions will not persist across restarts and are NOT shared across replicas "
            + "(state written on one pod is invisible to the others). Register "
            + "Granit.Notifications.EntityFrameworkCore for durable, multi-replica storage.")]
    public static partial void InMemoryStoresActiveInNonDevelopment(ILogger logger);
}
