namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to update a contact's identity fields.</summary>
public sealed record PartyUpdateRequest(
    string Name,
    string? Website = null,
    string? Language = null,
    string? Timezone = null);
