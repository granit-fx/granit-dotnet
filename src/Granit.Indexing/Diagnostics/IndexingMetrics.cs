using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Indexing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the indexing module.
/// Meter: <c>Granit.Indexing</c>.
/// </summary>
/// <remarks>
/// All counters carry a <c>tenant_id</c> tag coalesced to <c>"global"</c> when no tenant
/// context is active. <c>backend_hit_count</c> is the only telemetry-bound signal the
/// public contract exposes for authorization-induced backend amplification — it is
/// deliberately tenant-scoped (never principal-scoped) so it cannot serve as a
/// per-principal oracle for an attacker.
/// </remarks>
public sealed class IndexingMetrics
{
    public const string MeterName = "Granit.Indexing";

    internal const string TenantIdTag = "tenant_id";
    internal const string GlobalTenant = "global";

    private readonly Counter<long> _entryIndexed;
    private readonly Counter<long> _entryFailed;
    private readonly Counter<long> _searchQueries;
    private readonly Histogram<double> _searchLatency;
    private readonly Counter<long> _authorizationFiltered;
    private readonly Counter<long> _emptyResultThrottled;
    private readonly Counter<long> _backendHitCount;
    private readonly Counter<long> _aiInjectionAttempts;

    public IndexingMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _entryIndexed = meter.CreateCounter<long>(
            "granit.indexing.entry.indexed",
            description: "Number of entries successfully written to an index backend.");

        _entryFailed = meter.CreateCounter<long>(
            "granit.indexing.entry.failed",
            description: "Number of entries whose indexing failed (backend error or validation).");

        _searchQueries = meter.CreateCounter<long>(
            "granit.indexing.search.queries",
            description: "Number of search queries dispatched to a backend.");

        _searchLatency = meter.CreateHistogram<double>(
            "granit.indexing.search.latency",
            unit: "s",
            description: "End-to-end search latency, including authorization over-fetch loop.");

        _authorizationFiltered = meter.CreateCounter<long>(
            "granit.indexing.search.authorization_filtered",
            description: "Number of backend hits dropped by ISearchResultAuthorizer.FilterAsync.");

        _emptyResultThrottled = meter.CreateCounter<long>(
            "granit.indexing.search.empty_result_throttled",
            description: "Number of search responses synthetically throttled to mitigate principal-side existence oracles.");

        _backendHitCount = meter.CreateCounter<long>(
            "granit.indexing.search.backend_hit_count",
            description: "Total backend rows fetched across all over-fetch iterations. Internal telemetry — never returned to callers per-principal.");

        _aiInjectionAttempts = meter.CreateCounter<long>(
            "granit.indexing.search.ai.injection_attempt",
            description: "Number of search queries flagged by an AI-side prompt-injection heuristic (populated by AI providers).");
    }

    public void RecordEntryIndexed(string? tenantId, string backend)
    {
        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("backend", backend),
        ];
        _entryIndexed.Add(1, tags);
    }

    public void RecordEntryFailed(string? tenantId, string backend, string reason)
    {
        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("backend", backend),
            new("reason", reason),
        ];
        _entryFailed.Add(1, tags);
    }

    public void RecordSearchQuery(string? tenantId, string backend)
    {
        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("backend", backend),
        ];
        _searchQueries.Add(1, tags);
    }

    public void RecordSearchLatency(string? tenantId, string backend, double durationSeconds)
    {
        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("backend", backend),
        ];
        _searchLatency.Record(durationSeconds, tags);
    }

    public void RecordAuthorizationFiltered(string? tenantId, string backend, long filteredCount)
    {
        if (filteredCount <= 0)
        {
            return;
        }

        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("backend", backend),
        ];
        _authorizationFiltered.Add(filteredCount, tags);
    }

    public void RecordEmptyResultThrottled(string? tenantId)
    {
        TagList tags = [new(TenantIdTag, tenantId ?? GlobalTenant)];
        _emptyResultThrottled.Add(1, tags);
    }

    public void RecordBackendHits(string? tenantId, string backend, long count)
    {
        if (count <= 0)
        {
            return;
        }

        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("backend", backend),
        ];
        _backendHitCount.Add(count, tags);
    }

    public void RecordAiInjectionAttempt(string? tenantId, string detector)
    {
        TagList tags =
        [
            new(TenantIdTag, tenantId ?? GlobalTenant),
            new("detector", detector),
        ];
        _aiInjectionAttempts.Add(1, tags);
    }
}
