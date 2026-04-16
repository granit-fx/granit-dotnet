using Granit.Domain;

namespace Granit.Payments.Domain;

/// <summary>
/// Host-level configuration that activates a payment method type for the platform.
/// Each record maps a method type (e.g., <c>card</c>) to the provider that handles it
/// (e.g., <c>stripe</c>). Only active configurations are exposed to tenants via
/// <see cref="IPaymentProviderResolver"/>.
/// </summary>
public sealed class PaymentMethodConfiguration : AuditedEntity
{
    private PaymentMethodConfiguration() { }

    /// <summary>Creates a new payment method configuration (active by default).</summary>
    public static PaymentMethodConfiguration Create(
        Guid id, string providerName, string methodType, string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodType);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);

        return new PaymentMethodConfiguration
        {
            Id = id,
            ProviderName = providerName,
            MethodType = methodType,
            DisplayLabel = displayLabel,
            Category = PaymentMethods.GetCategory(methodType),
            IsActive = true,
        };
    }

    /// <summary>Payment method type identifier (e.g., <c>card</c>, <c>sepa_debit</c>).</summary>
    public string MethodType { get; private set; } = string.Empty;

    /// <summary>Provider name that handles this method (e.g., <c>stripe</c>, <c>sepa-transfer</c>).</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Human-readable label for the checkout UI.</summary>
    public string DisplayLabel { get; private set; } = string.Empty;

    /// <summary>Category for grouping in the checkout UI.</summary>
    public PaymentMethodCategory Category { get; private set; }

    /// <summary>Whether this method is currently active for the platform.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Activates this payment method for the platform.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Deactivates this payment method for the platform.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Updates the display label.</summary>
    public void UpdateDisplayLabel(string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);
        DisplayLabel = displayLabel;
    }
}
