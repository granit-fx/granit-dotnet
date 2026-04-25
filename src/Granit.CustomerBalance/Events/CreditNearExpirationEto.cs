using Granit.Events;

namespace Granit.CustomerBalance.Events;

/// <summary>
/// Published when a promotional credit is approaching its expiration date —
/// early-warning signal so apps can notify customers before the credit vanishes.
/// </summary>
/// <param name="BalanceAccountId">Owning balance account.</param>
/// <param name="TenantId">Owning tenant (denormalised for downstream consumers).</param>
/// <param name="CreditTransactionId">Original credit transaction id (idempotency key for downstream notifiers).</param>
/// <param name="ExpiringAmount">Remaining amount of the credit at the time of the scan.</param>
/// <param name="Currency">ISO 4217 currency code of the balance account.</param>
/// <param name="ExpiresAt">Scheduled expiration timestamp (UTC).</param>
/// <param name="DaysUntilExpiration">Whole days remaining (rounded toward zero from the fractional value).</param>
public sealed record CreditNearExpirationEto(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid CreditTransactionId,
    decimal ExpiringAmount,
    string Currency,
    DateTimeOffset ExpiresAt,
    int DaysUntilExpiration) : IIntegrationEvent;
