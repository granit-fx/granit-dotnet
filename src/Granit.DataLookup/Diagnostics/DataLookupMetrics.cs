using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.DataLookup.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the data-lookup module.
/// Meter: <c>Granit.DataLookup</c>.
/// </summary>
public sealed class DataLookupMetrics
{
    /// <summary>Name of the <see cref="Meter"/> owned by this module.</summary>
    public const string MeterName = "Granit.DataLookup";

    private readonly Counter<long> _searches;
    private readonly Counter<long> _resolves;
    private readonly Counter<long> _missingScope;
    private readonly Histogram<double> _searchDuration;

    /// <summary>Initializes a new <see cref="DataLookupMetrics"/> bound to <paramref name="meterFactory"/>.</summary>
    public DataLookupMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _searches = meter.CreateCounter<long>(
            "granit.data_lookup.search.executed",
            description: "Number of lookup search queries executed.");

        _resolves = meter.CreateCounter<long>(
            "granit.data_lookup.resolve.executed",
            description: "Number of single-value resolve lookups executed.");

        _missingScope = meter.CreateCounter<long>(
            "granit.data_lookup.scope.missing",
            description: "Lookup queries rejected because one or more declared scope keys were missing.");

        _searchDuration = meter.CreateHistogram<double>(
            "granit.data_lookup.search.duration",
            unit: "s",
            description: "Duration of lookup search execution in seconds.");
    }

    /// <summary>Records a successful search execution.</summary>
    public void RecordSearch(string? tenantId, string lookupName, double durationSeconds)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("lookup_name", lookupName),
        ];
        _searches.Add(1, tags);
        _searchDuration.Record(durationSeconds, tags);
    }

    /// <summary>Records a successful resolve-by-value execution.</summary>
    public void RecordResolve(string? tenantId, string lookupName)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("lookup_name", lookupName),
        ];
        _resolves.Add(1, tags);
    }

    /// <summary>Records a lookup query rejected for a missing scope key.</summary>
    public void RecordMissingScope(string? tenantId, string lookupName, string missingKey)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("lookup_name", lookupName),
            new("missing_key", missingKey),
        ];
        _missingScope.Add(1, tags);
    }
}
