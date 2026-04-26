using Granit.Events;

namespace Granit.CustomerBalance.Events;

/// <summary>Published when credit is deducted from a balance account.</summary>
public sealed record BalanceDebitedEto(
    Guid BalanceAccountId,
    Guid TenantId,
    Guid ContactId,
    decimal Amount,
    string Currency,
    Guid? ReferenceId,
    string? ReferenceType) : IIntegrationEvent;
