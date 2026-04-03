using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// Maps a plan to an external provider identifier (e.g., Stripe price_xxx, Mollie plan_yyy).
/// </summary>
public sealed class PlanExternalMapping : Entity
{
    private PlanExternalMapping() { }

    /// <summary>Creates a new external mapping.</summary>
    public static PlanExternalMapping Create(Guid id, string providerName, string externalId) =>
        new()
        {
            Id = id,
            ProviderName = providerName,
            ExternalId = externalId,
        };

    /// <summary>Payment provider name (e.g., <c>"stripe"</c>, <c>"mollie"</c>).</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>External identifier in the provider system (e.g., <c>"price_1234"</c>).</summary>
    public string ExternalId { get; private set; } = string.Empty;
}
