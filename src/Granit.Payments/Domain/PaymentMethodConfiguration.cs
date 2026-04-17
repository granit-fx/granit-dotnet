using Granit.Domain;

namespace Granit.Payments.Domain;

/// <summary>
/// Host-level activation record: declares that a payment method (provider + methodType)
/// is active on the platform. Tenants can only use methods that have an active record.
/// </summary>
/// <remarks>
/// <para>
/// This is a slim toggle entity — only <see cref="ProviderName"/>, <see cref="MethodType"/>
/// and <see cref="IsActive"/> are persisted. The display label and category are derived
/// at runtime from the corresponding <c>IPaymentProvider.SupportedMethods</c> descriptor
/// (category) and from <c>IStringLocalizer</c> (localized display label).
/// </para>
/// </remarks>
public sealed class PaymentMethodConfiguration : AuditedEntity
{
    private PaymentMethodConfiguration() { }

    /// <summary>Creates a new activation record. The record is active upon creation.</summary>
    public static PaymentMethodConfiguration Activate(Guid id, string providerName, string methodType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodType);

        return new PaymentMethodConfiguration
        {
            Id = id,
            ProviderName = providerName,
            MethodType = methodType,
            IsActive = true,
        };
    }

    /// <summary>Payment method type identifier (e.g., <c>card</c>, <c>sepa_debit</c>).</summary>
    public string MethodType { get; private set; } = string.Empty;

    /// <summary>Provider name that handles this method (e.g., <c>stripe</c>, <c>sepa-transfer</c>).</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Whether this method is currently active for the platform.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Marks this method as active for the platform.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Marks this method as inactive (tenants will no longer see it).</summary>
    public void Deactivate() => IsActive = false;
}
