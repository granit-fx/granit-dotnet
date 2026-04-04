namespace Granit.CustomerBalance.Endpoints.Dtos;

/// <summary>Transaction history entry.</summary>
public sealed record BalanceTransactionResponse(
    Guid Id,
    string Type,
    decimal Amount,
    string Source,
    string Reason,
    Guid? ReferenceId,
    string? ReferenceType,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
