using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Http.Idempotency.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the HTTP idempotency module.
/// Meter: <c>Granit.Http.Idempotency</c>.
/// </summary>
public sealed class IdempotencyMetrics
{
    public const string MeterName = "Granit.Http.Idempotency";

    private readonly Counter<long> _lockAcquired;
    private readonly Counter<long> _lockConflicts;
    private readonly Counter<long> _responsesReplayed;
    private readonly Counter<long> _hashMismatches;

    public IdempotencyMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _lockAcquired = meter.CreateCounter<long>(
            "granit.http.idempotency.lock.acquired",
            description: "Number of idempotency locks successfully acquired.");

        _lockConflicts = meter.CreateCounter<long>(
            "granit.http.idempotency.lock.conflict",
            description: "Number of idempotency lock conflicts (concurrent duplicate request).");

        _responsesReplayed = meter.CreateCounter<long>(
            "granit.http.idempotency.response.replayed",
            description: "Number of cached responses replayed from the idempotency store.");

        _hashMismatches = meter.CreateCounter<long>(
            "granit.http.idempotency.hash.mismatch",
            description: "Number of request payload hash mismatches (request mutation detected).");
    }

    public void RecordLockAcquired(string? tenantId) =>
        _lockAcquired.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordLockConflict(string? tenantId) =>
        _lockConflicts.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordResponseReplayed(string? tenantId, int statusCode) =>
        _responsesReplayed.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "status_code", statusCode.ToString() },
        });

    public void RecordHashMismatch(string? tenantId) =>
        _hashMismatches.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });
}
