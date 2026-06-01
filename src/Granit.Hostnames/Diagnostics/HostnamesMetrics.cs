using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Hostnames.Diagnostics;

/// <summary>
/// Metrics for Granit.Hostnames. Meter name: <c>Granit.Hostnames</c>.
/// </summary>
public sealed class HostnamesMetrics
{
    private readonly Counter<long> _resolved;
    private readonly Counter<long> _created;
    private readonly Counter<long> _deleted;

    /// <summary>
    /// Initializes a new instance of <see cref="HostnamesMetrics"/>.
    /// </summary>
    public HostnamesMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create("Granit.Hostnames");
        _resolved = meter.CreateCounter<long>(
            "granit.hostnames.managed_hostname.resolved",
            description: "Number of host-to-owner resolution lookups.");
        _created = meter.CreateCounter<long>(
            "granit.hostnames.managed_hostname.created",
            description: "Number of managed hostnames registered.");
        _deleted = meter.CreateCounter<long>(
            "granit.hostnames.managed_hostname.deleted",
            description: "Number of managed hostnames deleted.");
    }

    /// <summary>Records a host-to-owner resolution attempt.</summary>
    /// <param name="tenantId">Owning tenant id, or <c>null</c> for global hostnames.</param>
    /// <param name="resolved">Whether an active hostname was found.</param>
    public void RecordResolved(Guid? tenantId, bool resolved)
    {
        TagList tags = new()
        {
            { "tenant_id", tenantId?.ToString() ?? "global" },
            { "resolved", resolved },
        };
        _resolved.Add(1, tags);
    }

    /// <summary>Records a hostname registration.</summary>
    /// <param name="tenantId">Owning tenant id, or <c>null</c> for global hostnames.</param>
    public void RecordCreated(Guid? tenantId)
    {
        TagList tags = new() { { "tenant_id", tenantId?.ToString() ?? "global" } };
        _created.Add(1, tags);
    }

    /// <summary>Records a hostname deletion.</summary>
    /// <param name="tenantId">Owning tenant id, or <c>null</c> for global hostnames.</param>
    public void RecordDeleted(Guid? tenantId)
    {
        TagList tags = new() { { "tenant_id", tenantId?.ToString() ?? "global" } };
        _deleted.Add(1, tags);
    }
}
