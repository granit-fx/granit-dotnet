namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to add an email to a contact.</summary>
public sealed record PartyEmailRequest(string Address, bool IsPrimary = false, string? Label = null);
