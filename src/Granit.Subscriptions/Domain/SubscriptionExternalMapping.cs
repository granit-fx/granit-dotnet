using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// Maps a subscription to an external provider identifier (e.g., Stripe sub_xxx).
/// </summary>
public sealed class SubscriptionExternalMapping : Entity
{
    private SubscriptionExternalMapping() { }

    /// <summary>Creates a new external mapping.</summary>
    public static SubscriptionExternalMapping Create(
        Guid id, string providerName, string externalId) =>
        new()
        {
            Id = id,
            ProviderName = providerName,
            ExternalId = externalId,
        };

    /// <summary>Payment provider name (e.g., <c>"stripe"</c>, <c>"internal"</c>).</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>External identifier in the provider system (e.g., <c>"sub_1234"</c>).</summary>
    public string ExternalId { get; private set; } = string.Empty;
}
