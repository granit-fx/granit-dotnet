using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Payments.Domain;

/// <summary>A saved payment method (card, bank account, SEPA mandate).</summary>
public sealed class PaymentMethod : AuditedAggregateRoot, IMultiTenant
{
    private PaymentMethod() { }

    /// <summary>Creates a new payment method.</summary>
    public static PaymentMethod Create(
        Guid id, Guid tenantId, string type,
        string providerName, string providerMethodId,
        string displayLabel, DateTimeOffset? expiresAt = null) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Type = type,
            ProviderName = providerName,
            ProviderMethodId = providerMethodId,
            DisplayLabel = displayLabel,
            ExpiresAt = expiresAt,
            IsDefault = false,
        };

    /// <summary>Payment method type identifier (e.g., "card", "ideal", "sepa_debit").</summary>
    public string Type { get; private set; } = string.Empty;
    public string ProviderName { get; private set; } = string.Empty;
    public string ProviderMethodId { get; private set; } = string.Empty;

    [SensitiveData]
    public string DisplayLabel { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <inheritdoc />
    public Guid? TenantId { get; private set; }

    /// <inheritdoc />
    Guid? IMultiTenant.TenantId { get; set; }

    /// <summary>Sets this as the default payment method.</summary>
    public void SetDefault() => IsDefault = true;

    /// <summary>Unsets this as the default payment method.</summary>
    public void UnsetDefault() => IsDefault = false;
}
