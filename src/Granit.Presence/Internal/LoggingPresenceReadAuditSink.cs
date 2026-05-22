using Granit.Presence.Abstractions;
using Microsoft.Extensions.Logging;

namespace Granit.Presence.Internal;

internal sealed partial class LoggingPresenceReadAuditSink(
    ILogger<LoggingPresenceReadAuditSink> logger) : IPresenceReadAuditSink
{
    public Task RecordReadAsync(
        Guid callerUserId,
        int targetCount,
        int allowedCount,
        bool includesSelf,
        CancellationToken cancellationToken)
    {
        int deniedCount = Math.Max(0, targetCount - allowedCount);
        LogRead(logger, callerUserId, targetCount, allowedCount, deniedCount, includesSelf);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Presence read by {CallerUserId}: requested={TargetCount}, allowed={AllowedCount}, denied={DeniedCount}, includesSelf={IncludesSelf}.")]
    private static partial void LogRead(
        ILogger logger,
        Guid callerUserId,
        int targetCount,
        int allowedCount,
        int deniedCount,
        bool includesSelf);
}
