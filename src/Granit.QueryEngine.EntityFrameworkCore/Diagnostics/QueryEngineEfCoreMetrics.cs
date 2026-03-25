using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.QueryEngine.EntityFrameworkCore.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the QueryEngine EF Core engine.
/// Meter: <c>Granit.QueryEngine</c>.
/// </summary>
public sealed class QueryEngineEfCoreMetrics
{
    public const string MeterName = "Granit.QueryEngine";

    private readonly Counter<long> _queriesExecuted;
    private readonly Counter<long> _streamLimitsReached;
    private readonly Histogram<double> _queryDuration;

    public QueryEngineEfCoreMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _queriesExecuted = meter.CreateCounter<long>(
            "granit.query_engine.query.executed",
            description: "Number of queries executed by the query engine.");

        _streamLimitsReached = meter.CreateCounter<long>(
            "granit.query_engine.stream.limit_reached",
            description: "Number of streaming queries that hit the MaxStreamSize limit.");

        _queryDuration = meter.CreateHistogram<double>(
            "granit.query_engine.query.duration",
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
