using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Http.Resilience.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the HTTP resilience module.
/// Meter: <c>Granit.Http.Resilience</c>.
/// </summary>
public sealed class HttpResilienceMetrics
{
    public const string MeterName = "Granit.Http.Resilience";

    private readonly Counter<long> _retriesTriggered;
    private readonly Counter<long> _circuitBreakerStateChanges;
    private readonly Counter<long> _timeoutsOccurred;

    public HttpResilienceMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _retriesTriggered = meter.CreateCounter<long>(
            "granit.http.resilience.retry.triggered",
            description: "Number of retry attempts triggered by the resilience pipeline.");

        _circuitBreakerStateChanges = meter.CreateCounter<long>(
            "granit.http.resilience.circuit_breaker.state_changed",
            description: "Number of circuit breaker state transitions.");

        _timeoutsOccurred = meter.CreateCounter<long>(
            "granit.http.resilience.timeout.occurred",
            description: "Number of request timeouts in the resilience pipeline.");
    }

    public void RecordRetryTriggered(string? tenantId, string clientName, int attemptNumber) =>
        _retriesTriggered.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "client_name", clientName },
            { "attempt_number", attemptNumber.ToString() },
        });

    public void RecordCircuitBreakerStateChanged(string? tenantId, string clientName, string state) =>
        _circuitBreakerStateChanges.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "client_name", clientName },
            { "state", state },
        });

    public void RecordTimeoutOccurred(string? tenantId, string clientName) =>
        _timeoutsOccurred.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "client_name", clientName },
        });
}
