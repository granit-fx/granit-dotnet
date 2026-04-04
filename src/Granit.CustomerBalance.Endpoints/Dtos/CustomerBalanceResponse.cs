namespace Granit.CustomerBalance.Endpoints.Dtos;

/// <summary>Balance account summary.</summary>
public sealed record CustomerBalanceResponse(
    Guid BalanceAccountId,
    string Currency,
    decimal Balance,
    DateTimeOffset? UpdatedAt);
