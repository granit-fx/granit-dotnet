namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to update a contact's identity fields.</summary>
public sealed record ContactUpdateRequest(
    string Name,
    string? Website = null,
    string? Language = null,
    string? Timezone = null);
