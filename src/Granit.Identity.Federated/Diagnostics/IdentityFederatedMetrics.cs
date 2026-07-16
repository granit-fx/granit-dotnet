using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.Identity.Federated.Exceptions;

namespace Granit.Identity.Federated.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the federated identity providers.
/// Meter: <c>Granit.Identity.Federated</c>.
/// </summary>
/// <remarks>
/// Emitted by the graceful-degradation decorator so an operator can see the rate and
/// classification of upstream provider failures (<c>unauthorized</c>, <c>throttled</c>,
/// <c>transient</c>, <c>not_found</c>) that would otherwise be hidden behind a degraded
/// empty result. Tags are <c>snake_case</c> and always include <c>tenant_id</c>
/// (coalesced to <c>"global"</c>).
/// </remarks>
public sealed class IdentityFederatedMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Identity.Federated";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenantId = "global";

    private readonly Counter<long> _providerFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.federated.provider.failures",
        description: "Number of federated identity-provider operations that failed with a classified fault.");

    /// <summary>
    /// Records a classified provider failure observed by the graceful-degradation decorator.
    /// </summary>
    /// <param name="providerName">The provider that failed (e.g. <c>"keycloak"</c>).</param>
    /// <param name="operation">The provider operation (e.g. <c>"get_users"</c>).</param>
    /// <param name="category">The classified failure mode.</param>
    /// <param name="tenantId">The current tenant id, or <see langword="null"/> for the host partition.</param>
    public void RecordProviderFailure(
        string providerName, string operation, IdentityProviderFailureCategory category, string? tenantId)
    {
        TagList tags = new()
        {
            { "provider", providerName },
            { "operation", operation },
            { "category", CategoryTag(category) },
            { TenantIdTag, string.IsNullOrEmpty(tenantId) ? GlobalTenantId : tenantId },
        };
        _providerFailures.Add(1, tags);
    }

    private static string CategoryTag(IdentityProviderFailureCategory category) => category switch
    {
        IdentityProviderFailureCategory.Unauthorized => "unauthorized",
        IdentityProviderFailureCategory.NotFound => "not_found",
        IdentityProviderFailureCategory.Throttled => "throttled",
        IdentityProviderFailureCategory.Transient => "transient",
        _ => "unknown",
    };
}
