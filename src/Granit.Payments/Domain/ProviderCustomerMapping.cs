using Granit.DataProtection;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Payments.Domain;

/// <summary>
/// Maps a Granit tenant to a payment provider's customer ID.
/// </summary>
/// <remarks>
/// Payment providers (Stripe, Mollie) require a customer object to manage
/// saved payment methods. This mapping caches the relationship locally
/// to avoid expensive API lookups. Unique per (ProviderName, TenantId).
/// </remarks>
public sealed class ProviderCustomerMapping : Entity, IMultiTenant
{
    private ProviderCustomerMapping() { }

    /// <summary>Creates a new provider customer mapping.</summary>
    public static ProviderCustomerMapping Create(
        Guid id, string providerName, Guid tenantId, string providerCustomerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerCustomerId);

        return new ProviderCustomerMapping
        {
            Id = id,
            ProviderName = providerName,
            TenantId = tenantId,
            ProviderCustomerId = providerCustomerId,
        };
    }

    /// <summary>Payment provider name (e.g., "stripe", "mollie").</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>The Granit tenant ID.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>The provider's customer ID (e.g., cus_xxx for Stripe).</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string ProviderCustomerId { get; private set; } = string.Empty;
}
