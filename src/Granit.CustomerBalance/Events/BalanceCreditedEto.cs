using Granit.CustomerBalance.Domain;
using Granit.Events;

namespace Granit.CustomerBalance.Events;

/// <summary>Published when credit is added to a balance account.</summary>
public sealed record BalanceCreditedEto(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid ContactId,
    decimal Amount,
    string Currency,
    TransactionSource Source) : IIntegrationEvent;
