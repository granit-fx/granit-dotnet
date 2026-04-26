using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Customers.Domain;

/// <summary>Postal billing address attached to a <see cref="Customer"/>.</summary>
/// <remarks>
/// Owned value object — equality is structural over all components. Sensitive components
/// (street lines, postal code, VAT number) are flagged for the data-protection registry
/// so they are honoured by Privacy export/erasure handlers.
/// </remarks>
public sealed class BillingAddress : ValueObject
{
    private BillingAddress() { }

    /// <summary>Creates a new billing address.</summary>
    /// <param name="line1">Street address line 1 (required).</param>
    /// <param name="city">City (required).</param>
    /// <param name="postalCode">Postal / ZIP code (required).</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code (required).</param>
    /// <param name="companyName">Optional legal company name on the address.</param>
    /// <param name="line2">Optional second street line.</param>
    /// <param name="state">Optional administrative subdivision.</param>
    /// <param name="vatNumber">Optional VAT number for B2B reverse charge (e.g., <c>BE0123456789</c>).</param>
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

        return new BillingAddress
        {
            CompanyName = companyName,
            Line1 = line1,
            Line2 = line2,
            City = city,
            PostalCode = postalCode,
            State = state,
            Country = country,
            VatNumber = vatNumber,
        };
    }

    /// <summary>Optional legal company name shown on the address.</summary>
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

    /// <summary>ISO 3166-1 alpha-2 country code (required).</summary>
    public string Country { get; private set; } = string.Empty;

    /// <summary>VAT number for B2B reverse charge (e.g., <c>BE0123456789</c>).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
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
