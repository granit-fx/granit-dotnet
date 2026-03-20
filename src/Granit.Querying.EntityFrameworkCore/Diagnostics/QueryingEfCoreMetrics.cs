using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Querying.EntityFrameworkCore.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the querying EF Core engine.
/// Meter: <c>Granit.Querying.EntityFrameworkCore</c>.
/// </summary>
public sealed class QueryingEfCoreMetrics
{
    public const string MeterName = "Granit.Querying.EntityFrameworkCore";

    private readonly Counter<long> _queriesExecuted;
    private readonly Counter<long> _streamLimitsReached;
    private readonly Histogram<double> _queryDuration;

    public QueryingEfCoreMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _queriesExecuted = meter.CreateCounter<long>(
            "granit.querying.query.executed",
            description: "Number of queries executed by the query engine.");

        _streamLimitsReached = meter.CreateCounter<long>(
            "granit.querying.stream.limit_reached",
            description: "Number of streaming queries that hit the MaxStreamSize limit.");

        _queryDuration = meter.CreateHistogram<double>(
            "granit.querying.query.duration",
            unit: "s",
            description: "Duration of query execution in seconds.");
    }

    public void RecordQueryExecuted(string? tenantId, string entityType, string mode)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("entity_type", entityType),
            new("mode", mode),
        ];
        _queriesExecuted.Add(1, tags);
    }

    public void RecordStreamLimitReached(string? tenantId, string entityType)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("entity_type", entityType),
        ];
        _streamLimitsReached.Add(1, tags);
    }

    public void RecordQueryDuration(string? tenantId, string entityType, string mode, double durationSeconds)
    {
        TagList tags =
        [
            new("tenant_id", tenantId ?? "global"),
            new("entity_type", entityType),
            new("mode", mode),
        ];
        _queryDuration.Record(durationSeconds, tags);
    }
}
