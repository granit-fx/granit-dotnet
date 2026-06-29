using Granit.DataProtection;

namespace Granit.Domain.ValueObjects;

/// <summary>
/// Deliverability verdict for a postal address — the "is it real / deliverable?" axis, independent of
/// <see cref="AddressGeocoding"/> (the "can we place it on a map?" axis). A reusable owned value object
/// embedded by any address-holding entity.
/// </summary>
/// <remarks>
/// <para>
/// The verdict carries its <see cref="Source"/> (which evidence asserted it) and <see cref="VerifiedBy"/>
/// (which principal / module), so a confirmation is attributable rather than a bare status — important for
/// anti-repudiation, since <see cref="AddressVerificationStatus.DeliveryConfirmed"/> is the strongest
/// evidence but can originate from a system signal.
/// </para>
/// <para>
/// Persisted as flat columns via the framework's <c>MapAddressVerification</c> EF helper. Equality is
/// structural over all stored components.
/// </para>
/// </remarks>
public sealed class AddressVerification : ValueObject
{
    private AddressVerification() { }

    /// <summary>Initial state — no verification evidence yet.</summary>
    public static AddressVerification Unverified { get; } =
        new() { Status = AddressVerificationStatus.Unverified, Source = AddressVerificationSource.None };

    /// <summary>Creates a verification verdict with its provenance.</summary>
    /// <param name="status">The verdict.</param>
    /// <param name="source">Which evidence asserted it.</param>
    /// <param name="verifiedAt">When the verdict was recorded.</param>
    /// <param name="verifiedBy">The asserting principal / module (provenance), or <c>null</c>.</param>
    /// <param name="evidence">Optional evidence reference (e.g. a shipment or correspondence id).</param>
    public static AddressVerification Create(
        AddressVerificationStatus status,
        AddressVerificationSource source,
        DateTimeOffset verifiedAt,
        string? verifiedBy = null,
        string? evidence = null) =>
        new()
        {
            Status = status,
            Source = source,
            VerifiedAt = verifiedAt,
            VerifiedBy = verifiedBy,
            Evidence = evidence,
        };

    /// <summary>The deliverability verdict.</summary>
    public AddressVerificationStatus Status { get; init; } = AddressVerificationStatus.Unverified;

    /// <summary>Which evidence asserted the verdict (provenance).</summary>
    public AddressVerificationSource Source { get; init; } = AddressVerificationSource.None;

    /// <summary>When the verdict was recorded, or <c>null</c> when unverified.</summary>
    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>The asserting principal / module, or <c>null</c>. Provenance for anti-repudiation.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? VerifiedBy { get; init; }

    /// <summary>Optional evidence reference (e.g. shipment / correspondence id), or <c>null</c>.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? Evidence { get; init; }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Status;
        yield return Source;
        yield return VerifiedAt;
        yield return VerifiedBy;
        yield return Evidence;
    }
}
