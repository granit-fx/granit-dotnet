namespace Granit.CustomerBalance.Endpoints.Dtos;

/// <summary>Request to add a manual credit to a contact's balance.</summary>
public sealed record AdminCreditRequest(
    Guid PartyId,
    decimal Amount,
    string Currency,
    string Source,
    string Reason,
    DateTimeOffset? ExpiresAt);
