using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Invoicing.Domain;

/// <summary>Billing address for an invoice.</summary>
public sealed class BillingAddress : ValueObject
{
    private BillingAddress() { }

    /// <summary>Creates a new billing address.</summary>
    public static BillingAddress Create(
        string? companyName, string line1, string? line2,
        string city, string postalCode, string? state,
        string country, string? vatNumber = null) =>
        new()
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

    public string? CompanyName { get; private set; }
    [SensitiveData]
    public string Line1 { get; private set; } = string.Empty;

    [SensitiveData]
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    [SensitiveData]
    public string PostalCode { get; private set; } = string.Empty;
    public string? State { get; private set; }
    public string Country { get; private set; } = string.Empty;

    /// <summary>VAT number for B2B reverse charge (e.g., "BE0123456789").</summary>
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
