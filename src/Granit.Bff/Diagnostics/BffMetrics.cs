using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Bff.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the BFF module.
/// Meter: <c>Granit.Bff</c>.
/// </summary>
#pragma warning disable GRSEC003 // Metric names contain "token" — refers to counter names, not secrets
public sealed class BffMetrics
{
    public const string MeterName = "Granit.Bff";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _logins;
    private readonly Counter<long> _logouts;
    private readonly Counter<long> _tokenRefreshes;
    private readonly Counter<long> _proxyRequests;
    private readonly Counter<long> _proxyErrors;
    private readonly Counter<long> _csrfRejections;

    public BffMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _logins = meter.CreateCounter<long>(
            "granit.bff.logins",
            description: "Number of successful BFF logins.");

        _logouts = meter.CreateCounter<long>(
            "granit.bff.logouts",
            description: "Number of BFF logouts.");

        _tokenRefreshes = meter.CreateCounter<long>(
            "granit.bff.token.refreshes",
            description: "Number of silent token refreshes performed by the BFF proxy.");

        _proxyRequests = meter.CreateCounter<long>(
            "granit.bff.proxy.requests",
            description: "Number of requests proxied through BFF.");

        _proxyErrors = meter.CreateCounter<long>(
            "granit.bff.proxy.errors",
            description: "Number of proxy errors (upstream failures, missing sessions).");

        _csrfRejections = meter.CreateCounter<long>(
            "granit.bff.csrf.rejections",
            description: "Number of requests rejected due to invalid or missing CSRF token.");
    }

    public void RecordLogin(string? tenantId) =>
        _logins.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    public void RecordLogout(string? tenantId) =>
        _logouts.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    public void RecordTokenRefresh(string? tenantId) =>
        _tokenRefreshes.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    public void RecordProxyRequest(string? tenantId) =>
        _proxyRequests.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    public void RecordProxyError(string? tenantId, string reason) =>
        _proxyErrors.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
        });

    public void RecordCsrfRejection(string? tenantId) =>
        _csrfRejections.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });
}
#pragma warning restore GRSEC003
