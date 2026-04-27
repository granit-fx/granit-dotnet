using Granit.Events;

namespace Granit.CustomerBalance.Events;

/// <summary>
/// Published when a transaction transitions a balance account's running total from a
/// strictly positive amount to exactly zero — transition-driven, not state-driven, so a
/// no-op recompute that keeps the balance at zero does not re-emit the event.
/// </summary>
/// <param name="BalanceAccountId">Balance account that has just been depleted.</param>
/// <param name="TenantId">Tenant owning the balance account.</param>
/// <param name="PartyId">Party (end user) who owns the balance.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
/// <param name="DepletedAt">Timestamp of the transaction that depleted the balance.</param>
public sealed record BalanceDepletedEto(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    string Currency,
    DateTimeOffset DepletedAt) : IIntegrationEvent;
