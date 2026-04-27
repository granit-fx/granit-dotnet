using Granit.Events;

namespace Granit.CustomerBalance.Events;

/// <summary>Published when a promotional credit expires.</summary>
public sealed record CreditExpiredEto(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    decimal Amount,
    string Currency) : IIntegrationEvent;
