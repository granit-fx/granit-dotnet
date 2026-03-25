using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Authentication.ApiKeys.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the API key authentication module.
/// Meter: <c>Granit.Authentication.ApiKeys</c>.
/// </summary>
public sealed class ApiKeysMetrics
{
    public const string MeterName = "Granit.Authentication.ApiKeys";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _authenticationsSucceeded;
    private readonly Counter<long> _authenticationsFailed;
    private readonly Counter<long> _keysCreated;
    private readonly Counter<long> _keysRevoked;

    public ApiKeysMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _authenticationsSucceeded = meter.CreateCounter<long>(
            "granit.authentication.api_keys.authentication.succeeded",
            description: "Number of successful API key authentications.");

        _authenticationsFailed = meter.CreateCounter<long>(
            "granit.authentication.api_keys.authentication.failed",
            description: "Number of failed API key authentications.");

        _keysCreated = meter.CreateCounter<long>(
            "granit.authentication.api_keys.key.created",
            description: "Number of API keys created.");

        _keysRevoked = meter.CreateCounter<long>(
            "granit.authentication.api_keys.key.revoked",
            description: "Number of API keys revoked.");
    }

    public void RecordAuthenticationSucceeded(string? tenantId) =>
        _authenticationsSucceeded.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordAuthenticationFailed(string? tenantId, string reason) =>
        _authenticationsFailed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "reason", reason },
        });

    public void RecordKeyCreated(string? tenantId) =>
        _keysCreated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordKeyRevoked(string? tenantId) =>
        _keysRevoked.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}
