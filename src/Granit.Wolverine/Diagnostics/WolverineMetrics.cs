using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Wolverine.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Wolverine messaging module.
/// Meter: <c>Granit.Wolverine</c>.
/// </summary>
public sealed class WolverineMetrics
{
    public const string MeterName = "Granit.Wolverine";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _messagesDispatched;
    private readonly Counter<long> _messagesHandled;
    private readonly Counter<long> _retriesExhausted;
    private readonly Counter<long> _claimCheckStored;
    private readonly Counter<long> _claimCheckRetrieved;

    public WolverineMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _messagesDispatched = meter.CreateCounter<long>(
            "granit.wolverine.messages.dispatched",
            description: "Number of messages dispatched via the scoped sender.");

        _messagesHandled = meter.CreateCounter<long>(
            "granit.wolverine.messages.handled",
            description: "Number of messages handled with restored context (tenant + user).");

        _retriesExhausted = meter.CreateCounter<long>(
            "granit.wolverine.retries.exhausted",
            description: "Number of messages that exhausted all retry attempts.");

        _claimCheckStored = meter.CreateCounter<long>(
            "granit.wolverine.claimcheck.stored",
            description: "Number of payloads stored via the claim check pattern.");

        _claimCheckRetrieved = meter.CreateCounter<long>(
            "granit.wolverine.claimcheck.retrieved",
            description: "Number of payloads retrieved via the claim check pattern.");
    }

    public void RecordMessageDispatched(string? tenantId) =>
        _messagesDispatched.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordMessageHandled(string? tenantId, string messageType) =>
        _messagesHandled.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "message_type", messageType },
        });

    public void RecordRetriesExhausted(string? tenantId, string messageType) =>
        _retriesExhausted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "message_type", messageType },
        });

    public void RecordClaimCheckStored(string? tenantId) =>
        _claimCheckStored.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordClaimCheckRetrieved(string? tenantId) =>
        _claimCheckRetrieved.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}
