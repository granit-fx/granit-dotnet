using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Parties.Domain;

/// <summary>
/// Customer-specific tax classification — reverse-charge under EU B2B intra-EU rules,
/// blanket VAT exemption (NGO, public body, charity), or simply "standard" (no special
/// status). Owned value object on <see cref="Party"/>; equality is structural.
/// </summary>
/// <remarks>
/// <para>
/// Read by <c>Granit.Tax</c>'s <c>ITaxRateProvider.GetRateAsync(...)</c> when a
/// <c>contactId</c> is supplied: an exempt or reverse-charge contact yields a 0% rate
/// regardless of the country default, with the note flag indicating which mechanism
/// applied. Standard contacts fall back to the country / standard rate.
/// </para>
/// <para>
/// Defaults to <see cref="Standard"/>. Admins opt customers in via
/// <c>Party.SetTaxStatus(...)</c>; no data is implicitly converted from the
/// pre-Feature-5 schema.
/// </para>
/// </remarks>
public sealed class TaxStatus : ValueObject
{
    /// <summary>The do-nothing default: country / standard rate applies.</summary>
    public static TaxStatus Standard { get; } = new();

    private TaxStatus() { }

    /// <summary>Creates a customer tax status with the supplied flags.</summary>
    /// <param name="isExempt">VAT-exempt customer (NGO, public body, …) — overrides everything else.</param>
    /// <param name="reverseCharge">B2B intra-EU reverse charge applies — invoice carries the buyer's VAT number, no VAT line.</param>
    /// <param name="vatin">VAT identification number for B2B reverse charge (e.g. <c>"BE0123456789"</c>). Required when <paramref name="reverseCharge"/> is <c>true</c>.</param>
    /// <param name="evidenceBlobId">Optional <c>Granit.BlobStorage</c> reference to the supporting paperwork (exemption certificate, VAT registration excerpt).</param>
    public static TaxStatus Create(
        bool isExempt = false,
        bool reverseCharge = false,
        string? vatin = null,
        Guid? evidenceBlobId = null)
    {
        if (reverseCharge && string.IsNullOrWhiteSpace(vatin))
        {
            throw new ArgumentException(
                "Reverse-charge requires a VAT identification number on the buyer side.", nameof(vatin));
        }

        return new TaxStatus
        {
            IsExempt = isExempt,
            ReverseCharge = reverseCharge,
            Vatin = string.IsNullOrWhiteSpace(vatin) ? null : vatin,
            EvidenceBlobId = evidenceBlobId,
        };
    }

    /// <summary>Whether the customer is wholly VAT-exempt (overrides everything else).</summary>
    public bool IsExempt { get; private init; }

    /// <summary>Whether intra-EU B2B reverse charge applies — buyer accounts for the VAT.</summary>
    public bool ReverseCharge { get; private init; }

    /// <summary>VAT identification number on the buyer side (B2B reverse charge).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? Vatin { get; private init; }

    /// <summary>Optional pointer to the supporting paperwork in <c>Granit.BlobStorage</c>.</summary>
    public Guid? EvidenceBlobId { get; private init; }

    /// <summary>Returns <c>true</c> when this status forces a 0% rate (exempt or reverse-charge).</summary>
    public bool YieldsZeroRate => IsExempt || ReverseCharge;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsExempt;
        yield return ReverseCharge;
        yield return Vatin;
        yield return EvidenceBlobId;
    }
}
