namespace Granit.CustomerBalance.Endpoints.Dtos;

/// <summary>Request to add a manual credit to a tenant's balance.</summary>
public sealed record AdminCreditRequest(
    decimal Amount,
    string Currency,
    string Source,
    string Reason,
    DateTimeOffset? ExpiresAt);
