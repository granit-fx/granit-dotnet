using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to add a phone to a party.</summary>
public sealed record PartyPhoneRequest(
    PhoneKind Kind,
    string Number,
    bool IsPrimary = false,
    string? Label = null);
