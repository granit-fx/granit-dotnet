using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Localization.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the localization module.
/// Meter: <c>Granit.Localization</c>.
/// </summary>
public sealed class LocalizationMetrics
{
    public const string MeterName = "Granit.Localization";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _resourcesResolved;
    private readonly Counter<long> _overridesSet;
    private readonly Counter<long> _overridesRemoved;
    private readonly Counter<long> _cacheMisses;

    public LocalizationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _resourcesResolved = meter.CreateCounter<long>(
            "granit.localization.resource.resolved",
            description: "Number of localization resource resolutions.");

        _overridesSet = meter.CreateCounter<long>(
            "granit.localization.override.set",
            description: "Number of translation overrides created or updated.");

        _overridesRemoved = meter.CreateCounter<long>(
            "granit.localization.override.removed",
            description: "Number of translation overrides removed.");

        _cacheMisses = meter.CreateCounter<long>(
            "granit.localization.cache.miss",
            description: "Number of override cache misses requiring DB fetch.");
    }

    public void RecordResourceResolved(string? tenantId, string resourceName) =>
        _resourcesResolved.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "resource_name", resourceName },
        });

    public void RecordOverrideSet(string? tenantId, string resourceName, string culture) =>
        _overridesSet.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "resource_name", resourceName },
            { "culture", culture },
        });

    public void RecordOverrideRemoved(string? tenantId, string resourceName, string culture) =>
        _overridesRemoved.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "resource_name", resourceName },
            { "culture", culture },
        });

    public void RecordCacheMiss(string? tenantId, string resourceName, string culture) =>
        _cacheMisses.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "resource_name", resourceName },
            { "culture", culture },
        });
}
