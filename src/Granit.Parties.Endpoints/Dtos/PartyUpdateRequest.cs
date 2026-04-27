namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to update a party's identity fields.</summary>
/// <param name="Name">Display / legal name (required, max 256 chars).</param>
/// <param name="Website">Optional website URL.</param>
/// <param name="Language">Optional ISO locale (e.g. <c>"fr-BE"</c>).</param>
/// <param name="Timezone">Optional IANA timezone.</param>
/// <param name="InternalNotes">Optional admin-only free-form notes (max 8 000 chars). NEVER store PII.</param>
public sealed record PartyUpdateRequest(
    string Name,
    string? Website = null,
    string? Language = null,
    string? Timezone = null,
    string? InternalNotes = null);
