using Microsoft.Extensions.Logging;

namespace Granit.Http.UrlSafety.Diagnostics;

/// <summary>
/// Source-generated structured log messages for <c>Granit.Http.UrlSafety</c>.
/// </summary>
internal static partial class UrlSafetyLog
{
    // Warning, not Information: a blocked egress attempt (SSRF probe, metadata-endpoint
    // reach) is a security-relevant anomaly defenders must be able to alert on.
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "URL safety check blocked host '{Host}': {Kind} ({Reason})")]
    public static partial void UrlBlocked(ILogger logger, string host, UrlSafetyViolationKind kind, string reason);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "DNS resolution failed for host '{Host}' during URL safety check: {ErrorKind}")]
    public static partial void DnsResolutionFailed(ILogger logger, string host, string errorKind);
}
