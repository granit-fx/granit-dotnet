using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Settings.Diagnostics;

/// <summary>
/// Metrics for Granit.Settings write operations and cache invalidations.
/// </summary>
/// <remarks>
/// Meter name: <c>Granit.Settings</c>. All metric names follow the
/// <c>granit.settings.{entity}.{action}</c> convention.
/// </remarks>
public sealed class SettingsMetrics
{
    public const string MeterName = "Granit.Settings";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _valuesChanged;
    private readonly Counter<long> _valuesDeleted;
    private readonly Counter<long> _cacheInvalidations;

    public SettingsMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _valuesChanged = meter.CreateCounter<long>(
            "granit.settings.value.changed",
            description: "Number of setting values created or updated.");

        _valuesDeleted = meter.CreateCounter<long>(
            "granit.settings.value.deleted",
            description: "Number of setting values deleted.");

        _cacheInvalidations = meter.CreateCounter<long>(
            "granit.settings.cache.invalidated",
            description: "Number of setting cache entries invalidated after a write.");
    }

    public void RecordValueChanged(string? tenantId, string providerName, string settingName) =>
        _valuesChanged.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "provider_name", providerName },
            { "setting_name", settingName },
        });

    public void RecordValueDeleted(string? tenantId, string providerName, string settingName) =>
        _valuesDeleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "provider_name", providerName },
            { "setting_name", settingName },
        });

    public void RecordCacheInvalidated(string? tenantId, string providerName) =>
        _cacheInvalidations.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "provider_name", providerName },
        });
}
