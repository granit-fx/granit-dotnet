using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to add an address to a party.</summary>
public sealed record PartyAddressRequest(
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
