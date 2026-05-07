using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Parties.Domain;

/// <summary>
/// Canonical billing address attached to a <see cref="Party"/>. Distinct from the multi-typed
/// <see cref="PartyAddress"/> collection on purpose — billing carries legal weight (VAT number,
/// reverse-charge B2B rules, the address shown on the invoice as a legal document) that the
/// general postal-address concept does not. Owned by the parent aggregate; equality is structural
/// over every component.
/// </summary>
/// <remarks>
/// Snapshotted by <c>Granit.Invoicing.Domain.Invoice.Finalize</c> into
/// <c>Invoice.IssuedBillingAddressSnapshot</c> so that the address shown on a finalized invoice
/// stays stable forever, even if the party's billing address is later updated.
/// </remarks>
public sealed class BillingAddress : ValueObject
{
    private BillingAddress() { }

    /// <summary>Creates a new billing address.</summary>
    /// <param name="line1">Street address line 1 (required).</param>
    /// <param name="city">City (required).</param>
    /// <param name="postalCode">Postal / ZIP code (required).</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code (required, exactly 2 chars).</param>
    /// <param name="companyName">Optional legal company name shown on the invoice.</param>
    /// <param name="line2">Optional second street line.</param>
    /// <param name="state">Optional administrative subdivision (e.g., a US state).</param>
    /// <param name="vatNumber">Optional VAT number for B2B reverse charge (e.g., <c>"BE0123456789"</c>).</param>
    public static BillingAddress Create(
        string line1,
        string city,
        string postalCode,
        string country,
        string? companyName = null,
        string? line2 = null,
        string? state = null,
        string? vatNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        if (country.Length != 2)
        {
            throw new ArgumentException(
                "Country must be a 2-letter ISO 3166-1 alpha-2 code.", nameof(country));
        }

        return new BillingAddress
        {
            CompanyName = companyName,
            Line1 = line1,
            Line2 = line2,
            City = city,
            PostalCode = postalCode,
            State = state,
            Country = country.ToUpperInvariant(),
            VatNumber = vatNumber,
        };
    }

    /// <summary>Optional legal company name shown on the invoice.</summary>
    public string? CompanyName { get; private set; }

    /// <summary>Street address line 1 (required).</summary>
    [SensitiveData]
    public string Line1 { get; private set; } = string.Empty;

    /// <summary>Optional second street line.</summary>
    [SensitiveData]
    public string? Line2 { get; private set; }

    /// <summary>City (required).</summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>Postal / ZIP code (required).</summary>
    [SensitiveData]
    public string PostalCode { get; private set; } = string.Empty;

    /// <summary>Optional administrative subdivision (e.g., a US state).</summary>
    public string? State { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 country code (always upper-case after construction).</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>VAT number for B2B reverse charge (e.g., <c>"BE0123456789"</c>).</summary>
    /// <remarks>
    /// Classified <see cref="Sensitivity.Internal"/> — VAT numbers are public
    /// registration data: they appear on every invoice the company issues, are
    /// searchable in national registries (KBO/BCE, Companies House) and in VIES,
    /// and the field exists precisely because the value is publicly required for
    /// B2B reverse charge. No at-rest encryption mandate.
    /// </remarks>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string? VatNumber { get; private set; }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CompanyName;
        yield return Line1;
        yield return Line2;
        yield return City;
        yield return PostalCode;
        yield return State;
        yield return Country;
        yield return VatNumber;
    }
}
