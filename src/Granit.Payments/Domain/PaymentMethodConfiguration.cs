using System.Collections.Immutable;
using Granit.Domain;
using Granit.Payments.Contracts;

namespace Granit.Payments.Domain;

/// <summary>
/// Host-level activation record: declares that a payment method (provider + methodType)
/// is active on the platform. Tenants can only use methods that have an active record.
/// </summary>
/// <remarks>
/// <para>
/// The display label and category are derived at runtime from the corresponding
/// <c>IPaymentProvider</c> catalog (category) and from <c>IStringLocalizer</c>
/// (localized display label).
/// </para>
/// <para>
/// When admins activate a method they also snapshot the provider's declared
/// <see cref="PaymentMethodCapability"/> onto this record via
/// <see cref="SnapshotCapability"/>. The runtime availability filter reads this
/// snapshot — never the live provider catalog — so the hot checkout path is
/// deterministic, offline-capable, and does not hit the provider on every request.
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

    /// <summary>
    /// Snapshot of the countries supported by the provider for this method.
    /// <see langword="null"/> when no snapshot has been captured yet (legacy record);
    /// an empty set means the method is supported globally.
    /// </summary>
    public ImmutableHashSet<string>? SupportedCountries { get; private set; }

    /// <summary>
    /// Snapshot of the currencies supported by the provider for this method.
    /// <see langword="null"/> when no snapshot has been captured yet; empty = all currencies.
    /// </summary>
    public ImmutableHashSet<string>? SupportedCurrencies { get; private set; }

    /// <summary>
    /// Snapshot of the sequence modes supported by the provider for this method.
    /// <see langword="null"/> when no snapshot has been captured yet.
    /// </summary>
    public PaymentMethodSequenceType? SupportedSequenceTypes { get; private set; }

    /// <summary>
    /// Snapshot of the per-currency amount bounds declared by the provider for this method.
    /// <see langword="null"/> when no snapshot has been captured yet; empty dictionary = no bounds.
    /// </summary>
    public ImmutableDictionary<string, PaymentMethodAmountBound>? AmountBounds { get; private set; }

    /// <summary>Marks this method as active for the platform.</summary>
    public void Activate() => IsActive = true;

    /// <summary>Marks this method as inactive (tenants will no longer see it).</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Captures the provider's current capability for this method. Called by the admin
    /// activation and resync flows.
    /// </summary>
    public void SnapshotCapability(PaymentMethodCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        SupportedCountries = [.. capability.SupportedCountries];
        SupportedCurrencies = [.. capability.SupportedCurrencies];
        SupportedSequenceTypes = capability.SupportedSequenceTypes;
        AmountBounds = capability.AmountBounds.ToImmutableDictionary(StringComparer.Ordinal);
    }

    /// <summary>Drops the capability snapshot. Typically used when detaching a provider.</summary>
    public void ClearCapability()
    {
        SupportedCountries = null;
        SupportedCurrencies = null;
        SupportedSequenceTypes = null;
        AmountBounds = null;
    }

    /// <summary>
    /// Returns the current capability snapshot, or <see langword="null"/> when none has been
    /// captured yet. A null return means the runtime filter should treat this record as
    /// wildcard on every axis.
    /// </summary>
    public PaymentMethodCapability? GetCapabilitySnapshot()
    {
        if (SupportedCountries is null
            || SupportedCurrencies is null
            || SupportedSequenceTypes is null
            || AmountBounds is null)
        {
            return null;
        }

        return new PaymentMethodCapability(
            SupportedCountries,
            SupportedCurrencies,
            SupportedSequenceTypes.Value,
            AmountBounds);
    }
}
