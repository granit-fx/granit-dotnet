using Microsoft.Extensions.Logging;

namespace Granit.Notifications.MobilePush.Diagnostics;

/// <summary>
/// High-performance structured log messages for the mobile push channel.
/// </summary>
internal static partial class MobilePushLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit.Notifications.MobilePush is using the in-memory device token store — "
            + "device tokens will not persist across restarts and are NOT shared across replicas "
            + "(a token registered on one pod is invisible to the others, so pushes dispatched "
            + "elsewhere silently reach nobody). Register "
            + "Granit.Notifications.MobilePush.EntityFrameworkCore for durable, multi-replica storage.")]
    public static partial void InMemoryStoreActiveInNonDevelopment(ILogger logger);
}
