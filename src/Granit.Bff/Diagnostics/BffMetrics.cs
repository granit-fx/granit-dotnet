using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Bff.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the BFF module.
/// Meter: <c>Granit.Bff</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.bff.{entity}.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </remarks>
#pragma warning disable GRSEC003 // Metric names contain "token" — refers to counter names, not secrets
public sealed class BffMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Bff";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _logins = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.bff.session.login",
        description: "Number of successful BFF logins.");

    private readonly Counter<long> _logouts = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.bff.session.logout",
        description: "Number of BFF logouts.");

    private readonly Counter<long> _tokenRefreshes = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.bff.token.refreshes",
        description: "Number of silent token refreshes performed by the BFF proxy.");

    private readonly Counter<long> _proxyRequests = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.bff.proxy.requests",
        description: "Number of requests proxied through BFF.");

    private readonly Counter<long> _proxyErrors = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.bff.proxy.errors",
        description: "Number of proxy errors (upstream failures, missing sessions).");

    private readonly Counter<long> _csrfRejections = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.bff.csrf.rejections",
        description: "Number of requests rejected due to invalid or missing CSRF token.");

    /// <summary>Records a successful BFF login.</summary>
    public void RecordLogin(string? tenantId) =>
        _logins.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    /// <summary>Records a BFF logout.</summary>
    public void RecordLogout(string? tenantId) =>
        _logouts.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    /// <summary>Records a silent token refresh.</summary>
    public void RecordTokenRefresh(string? tenantId) =>
        _tokenRefreshes.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    /// <summary>Records a proxied request.</summary>
    public void RecordProxyRequest(string? tenantId) =>
        _proxyRequests.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });

    /// <summary>Records a proxy error.</summary>
    public void RecordProxyError(string? tenantId, string reason) =>
        _proxyErrors.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
        });

    /// <summary>Records a CSRF rejection.</summary>
    public void RecordCsrfRejection(string? tenantId) =>
        _csrfRejections.Add(1, new TagList { { TagTenantId, tenantId ?? DefaultTenant } });
}
#pragma warning restore GRSEC003
