namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to add an email to a party.</summary>
public sealed record PartyEmailRequest(string Address, bool IsPrimary = false, string? Label = null);
