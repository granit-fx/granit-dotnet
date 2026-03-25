using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Oidc.TokenManagement.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the token management module.
/// Meter: <c>Granit.Oidc.TokenManagement</c>.
/// </summary>
internal sealed class TokenManagementMetrics
{
    public const string MeterName = "Granit.Oidc.TokenManagement";

    private const string TagTenantId = "tenant_id";
    private const string TagGrantType = "grant_type";
    private const string TagClientName = "client_name";
    private const string TagErrorType = "error_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _tokenRequests;
    private readonly Counter<long> _cacheHits;
    private readonly Counter<long> _cacheMisses;
    private readonly Counter<long> _revocations;
    private readonly Counter<long> _errors;

    public TokenManagementMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _tokenRequests = meter.CreateCounter<long>(
            "granit.oidc.token_management.request.sent",
            description: "Number of token endpoint requests sent.");

        _cacheHits = meter.CreateCounter<long>(
            "granit.oidc.token_management.cache.hit",
            description: "Number of client credentials token cache hits.");

        _cacheMisses = meter.CreateCounter<long>(
            "granit.oidc.token_management.cache.miss",
            description: "Number of client credentials token cache misses.");

        _revocations = meter.CreateCounter<long>(
            "granit.oidc.token_management.revocation.sent",
            description: "Number of token revocations sent.");

        _errors = meter.CreateCounter<long>(
            "granit.oidc.token_management.error.occurred",
            description: "Number of token endpoint errors.");
    }

    public void RecordTokenRequest(string? tenantId, string grantType, string? clientName) =>
        _tokenRequests.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagGrantType, grantType },
            { TagClientName, clientName ?? "unknown" },
        });

    public void RecordCacheHit(string? tenantId, string? clientName) =>
        _cacheHits.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagClientName, clientName ?? "unknown" },
        });

    public void RecordCacheMiss(string? tenantId, string? clientName) =>
        _cacheMisses.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagClientName, clientName ?? "unknown" },
        });

    public void RecordRevocation(string? tenantId) =>
        _revocations.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordError(string? tenantId, string errorType) =>
        _errors.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagErrorType, errorType },
        });
}
