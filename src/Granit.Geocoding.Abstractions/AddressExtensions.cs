using Granit.Domain.ValueObjects;

namespace Granit.Geocoding;

/// <summary>
/// Conversions from the domain <see cref="Address"/> value object to the geocoding
/// <see cref="PostalAddress"/> input contract.
/// </summary>
public static class AddressExtensions
{
    /// <summary>
    /// Projects a domain <see cref="Address"/> onto the looser geocoding <see cref="PostalAddress"/>
    /// input — <see cref="Address.Street1"/> → <see cref="PostalAddress.Street"/>,
    /// <see cref="Address.City"/> → <see cref="PostalAddress.Locality"/>. This is the single shared
    /// conversion so every consumer geocodes addresses identically.
    /// </summary>
    /// <param name="address">The domain address to convert.</param>
    /// <returns>A geocoding input carrying the street, postal code, locality and country.</returns>
    public static PostalAddress ToPostalAddress(this Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return new PostalAddress(address.Street1, address.PostalCode, address.City, address.Country);
    }
}
