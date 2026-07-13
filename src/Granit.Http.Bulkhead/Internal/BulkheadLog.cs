using Microsoft.Extensions.Logging;

namespace Granit.Http.Bulkhead.Internal;

/// <summary>
/// Source-generated logger messages for the bulkhead module.
/// </summary>
internal static partial class BulkheadLog
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Bulkhead lease acquired for policy '{PolicyName}' (tenant: {TenantId}).")]
    public static partial void LogLeaseAcquired(ILogger logger, string policyName, string? tenantId);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Bulkhead lease released for policy '{PolicyName}' (tenant: {TenantId}).")]
    public static partial void LogLeaseReleased(ILogger logger, string policyName, string? tenantId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Bulkhead rejected for policy '{PolicyName}' (tenant: {TenantId}). PermitLimit: {PermitLimit}, QueueLimit: {QueueLimit}.")]
    public static partial void LogBulkheadRejected(ILogger logger, string policyName, string? tenantId, int permitLimit, int queueLimit);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Bulkhead bypassed for policy '{PolicyName}' (reason: {Reason}, user: {UserId}).")]
    public static partial void LogBypassApplied(ILogger logger, string policyName, string reason, string? userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Bulkhead policy '{PolicyName}' is not configured — endpoint/handler will execute without isolation. Check the 'Http:Bulkhead:Policies' section in appsettings.")]
    public static partial void LogUnknownPolicy(ILogger logger, string policyName);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Bulkhead acquire abandoned by caller for policy '{PolicyName}' (tenant: {TenantId}).")]
    public static partial void LogAcquireAbandoned(ILogger logger, string policyName, string? tenantId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Evicted {Count} idle bulkhead limiters.")]
    public static partial void LogIdleLimitersEvicted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "UseFeatureBasedQuotas is enabled but IFeatureChecker is not registered. Falling back to static PermitLimit from configuration.")]
    public static partial void LogFeatureCheckerMissing(ILogger logger);
}
