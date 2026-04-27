using Granit.Events;

namespace Granit.CustomerBalance.Events;

/// <summary>
/// Published when a promotional credit is approaching its expiration date — emitted
/// proactively by the daily expiration scanner once the credit's <c>ExpiresAt</c> falls
/// within the configured <c>ExpirationLeadTimeDays</c> window.
/// </summary>
/// <remarks>
/// Per-credit dedupe is enforced upstream via
/// <c>BalanceTransaction.LastExpirationNotifiedAt</c> — the scanner stamps the
/// transaction once the Eto has been published so the same credit is not re-alerted
/// every run during the lead-time window.
/// </remarks>
/// <param name="BalanceAccountId">Balance account holding the credit.</param>
/// <param name="TenantId">Tenant owning the balance account.</param>
/// <param name="PartyId">Party (end user) who owns the balance.</param>
/// <param name="CreditId">Identifier of the promotional credit transaction.</param>
/// <param name="Amount">Original credited amount, positive.</param>
/// <param name="Currency">ISO 4217 currency code of the balance.</param>
/// <param name="ExpiresAt">Scheduled expiration timestamp of the credit.</param>
public sealed record CreditExpiringEto(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid PartyId,
    Guid CreditId,
    decimal Amount,
    string Currency,
    DateTimeOffset ExpiresAt) : IIntegrationEvent;
