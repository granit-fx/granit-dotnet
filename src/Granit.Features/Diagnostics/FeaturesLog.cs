using Microsoft.Extensions.Logging;

namespace Granit.Features.Diagnostics;

/// <summary>
/// High-performance structured log messages for the Features module.
/// </summary>
internal static partial class FeaturesLog
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit.Features is using InMemoryFeatureStore — feature overrides will not "
            + "persist across application restarts and have no audit trail. Register "
            + "Granit.Features.EntityFrameworkCore to enable persistent storage.")]
    public static partial void InMemoryStoreActiveInNonDevelopment(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Feature override changed: '{FeatureName}' for tenant {TenantId} — {OldValue} → {NewValue}")]
    public static partial void FeatureOverrideChanged(
        ILogger logger, string featureName, string tenantId, string? oldValue, string? newValue);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Feature limit exceeded: '{FeatureName}' for tenant {TenantId} — current {CurrentCount} >= limit {Limit}")]
    public static partial void FeatureLimitExceeded(
        ILogger logger, string featureName, string tenantId, long currentCount, long limit);
}
