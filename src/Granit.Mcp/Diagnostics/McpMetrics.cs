using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Mcp.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit MCP module.
/// Meter: <c>Granit.Mcp</c>.
/// </summary>
public sealed class McpMetrics
{
    public const string MeterName = "Granit.Mcp";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _toolsInvoked;
    private readonly Counter<long> _resourcesRead;
    private readonly UpDownCounter<long> _sessionsActive;
    private readonly Histogram<double> _requestDuration;

    public McpMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _toolsInvoked = meter.CreateCounter<long>(
            "granit.mcp.tools.invoked",
            description: "Number of MCP tool invocations.");

        _resourcesRead = meter.CreateCounter<long>(
            "granit.mcp.resources.read",
            description: "Number of MCP resource reads.");

        _sessionsActive = meter.CreateUpDownCounter<long>(
            "granit.mcp.sessions.active",
            description: "Number of active MCP sessions.");

        _requestDuration = meter.CreateHistogram<double>(
            "granit.mcp.request.duration",
            unit: "s",
            description: "Duration of MCP requests in seconds.");
    }

    public void RecordToolInvoked(string? tenantId, string toolName, string status) =>
        _toolsInvoked.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "tool_name", toolName },
            { "status", status },
        });

    public void RecordResourceRead(string? tenantId, string resourceUri) =>
        _resourcesRead.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "resource_uri", resourceUri },
        });

    public void RecordSessionStarted(string? tenantId, string transport) =>
        _sessionsActive.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "transport", transport },
        });

    public void RecordSessionEnded(string? tenantId, string transport) =>
        _sessionsActive.Add(-1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "transport", transport },
        });

    public void RecordRequestDuration(string? tenantId, string method, TimeSpan duration) =>
        _requestDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "method", method },
        });
}
