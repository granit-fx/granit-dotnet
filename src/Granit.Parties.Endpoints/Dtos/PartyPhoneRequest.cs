using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to add a phone to a contact.</summary>
public sealed record PartyPhoneRequest(
    PhoneKind Kind,
    string Number,
    bool IsPrimary = false,
    string? Label = null);
