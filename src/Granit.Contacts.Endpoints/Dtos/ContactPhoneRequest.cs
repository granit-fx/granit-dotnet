using Granit.Contacts.Domain;

namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to add a phone to a contact.</summary>
public sealed record ContactPhoneRequest(
    PhoneKind Kind,
    string Number,
    bool IsPrimary = false,
    string? Label = null);
