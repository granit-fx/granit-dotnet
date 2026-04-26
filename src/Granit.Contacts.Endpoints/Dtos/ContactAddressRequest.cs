using Granit.Contacts.Domain;

namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to add an address to a contact.</summary>
public sealed record ContactAddressRequest(
    AddressKind Kind,
    string Line1,
    string City,
    string PostalCode,
    string Country,
    string? CompanyName = null,
    string? Line2 = null,
    string? State = null,
    bool IsDefault = false,
    string? Label = null);
