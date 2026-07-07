using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Wolverine.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Wolverine messaging module.
/// Meter: <c>Granit.Wolverine</c>.
/// </summary>
/// <remarks>
/// These counters complement Wolverine's native <c>Wolverine</c> meter (execution
/// time, dead-letter, retry counters) with Granit-specific signals: tenant-tagged
/// dispatch/handling volume and untenanted-envelope observability. Failure and
/// retry observability is intentionally left to the native Wolverine meter.
/// </remarks>
public sealed class WolverineMetrics
{
    public const string MeterName = "Granit.Wolverine";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _messagesDispatched;
    private readonly Counter<long> _messagesHandled;
    private readonly Counter<long> _envelopeNoTenant;

    public WolverineMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _messagesDispatched = meter.CreateCounter<long>(
            "granit.wolverine.messages.dispatched",
            description: "Number of messages dispatched via the Granit command senders "
                + "(ICommandSender / WolverineScopedSender).");

        _messagesHandled = meter.CreateCounter<long>(
            "granit.wolverine.messages.handled",
            description: "Number of messages entering handler execution with a restored tenant context.");

        _envelopeNoTenant = meter.CreateCounter<long>(
            "granit.wolverine.envelope.no_tenant",
            description:
                "Number of received envelopes that did not carry an X-Tenant-Id header. "
                + "Tagged with `message_type` and `outcome` (`marked` = the type carries "
                + "[CrossTenantMessage]; `unmarked` = no annotation, indicates a producer "
                + "that did not propagate the tenant or a message type that should be "
                + "explicitly marked host-scope). Authorization is enforced downstream by "
                + "per-(user, tenant, action) permission checks at the handler boundary.");
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

    /// <summary>
    /// Records an incoming envelope that did not carry an <c>X-Tenant-Id</c> header.
    /// </summary>
    /// <param name="messageType">The CLR name of the message type.</param>
    /// <param name="outcome">
    /// <c>"marked"</c> when the message type carries
    /// <see cref="CrossTenantMessageAttribute"/>; <c>"unmarked"</c> otherwise.
    /// </param>
    public void RecordEnvelopeWithoutTenant(string messageType, string outcome) =>
        _envelopeNoTenant.Add(1, new TagList
        {
            { "message_type", messageType },
            { "outcome", outcome },
        });
}
