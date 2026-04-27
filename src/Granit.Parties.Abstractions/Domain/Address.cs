using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Parties.Domain;

/// <summary>Postal address attached to a <see cref="Party"/> (single billing address for the MVP).</summary>
/// <remarks>
/// Owned value object — equality is structural over all components. Sensitive components
/// (street lines, postal code) are flagged for the data-protection registry so the
/// Privacy export/erasure handlers honour them automatically.
/// <para>
/// Multiple typed addresses (billing / delivery / other) are deferred to a future epic.
/// </para>
/// </remarks>
public sealed class Address : ValueObject
{
    private Address() { }

    /// <summary>Creates a new address.</summary>
    /// <param name="line1">Street address line 1 (required).</param>
    /// <param name="city">City (required).</param>
    /// <param name="postalCode">Postal / ZIP code (required).</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code (required, exactly 2 chars).</param>
    /// <param name="companyName">Optional legal company name on the address.</param>
    /// <param name="line2">Optional second street line.</param>
    /// <param name="state">Optional administrative subdivision.</param>
    public static Address Create(
        string line1,
        string city,
        string postalCode,
        string country,
        string? companyName = null,
        string? line2 = null,
        string? state = null)
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

        return new Address
        {
            CompanyName = companyName,
            Line1 = line1,
            Line2 = line2,
            City = city,
            PostalCode = postalCode,
            State = state,
            Country = country.ToUpperInvariant(),
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

    /// <summary>ISO 3166-1 alpha-2 country code (always upper-case after construction).</summary>
    public string Country { get; private set; } = string.Empty;

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
    }
}
