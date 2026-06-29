using System.Text.Json.Serialization;
using Granit.DataProtection;

namespace Granit.Domain.ValueObjects;

/// <summary>Postal address value object.</summary>
/// <remarks>
/// Owned value object — equality is structural over all components. Sensitive components
/// (street lines, postal code) are flagged for the data-protection registry so the
/// Privacy export/erasure handlers honour them automatically.
/// </remarks>
public sealed class Address : ValueObject
{
    // [JsonConstructor] enables STJ deserialization via the framework's RemoveValueObjectEntityTypes
    // convention, which stores multi-field ValueObject types as JSON columns.
    [JsonConstructor]
    private Address() { }

    /// <summary>Creates a new address.</summary>
    /// <param name="street1">Street address line 1 (required).</param>
    /// <param name="city">City (required).</param>
    /// <param name="postalCode">Postal / ZIP code (required).</param>
    /// <param name="country">ISO 3166-1 alpha-2 country code (required, exactly 2 chars).</param>
    /// <param name="street2">Optional second street line.</param>
    /// <param name="state">Optional administrative subdivision.</param>
    /// <param name="deliveryPointType">Optional kind of delivery point (street, PO box, …).</param>
    public static Address Create(
        string street1,
        string city,
        string postalCode,
        string country,
        string? street2 = null,
        string? state = null,
        AddressDeliveryPointType? deliveryPointType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street1);
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
            Street1 = street1,
            Street2 = street2,
            City = city,
            PostalCode = postalCode,
            State = state,
            Country = country.ToUpperInvariant(),
            DeliveryPointType = deliveryPointType,
        };
    }

    /// <summary>Street address line 1 (required).</summary>
    [SensitiveData]
    public string Street1 { get; init; } = string.Empty;

    /// <summary>Optional second street line.</summary>
    [SensitiveData]
    public string? Street2 { get; init; }

    /// <summary>City (required).</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>Postal / ZIP code (required).</summary>
    [SensitiveData]
    public string PostalCode { get; init; } = string.Empty;

    /// <summary>Optional administrative subdivision (e.g., a US state).</summary>
    public string? State { get; init; }

    /// <summary>ISO 3166-1 alpha-2 country code (always upper-case after construction).</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>
    /// Optional kind of delivery point (street, PO box, …). Drives deliverability semantics — a PO box can
    /// never be confirmed by a courier delivery. <c>null</c> when unknown.
    /// </summary>
    public AddressDeliveryPointType? DeliveryPointType { get; init; }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street1;
        yield return Street2;
        yield return City;
        yield return PostalCode;
        yield return State;
        yield return Country;
        yield return DeliveryPointType;
    }
}
