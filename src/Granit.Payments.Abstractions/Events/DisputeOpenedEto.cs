using Granit.Events;

namespace Granit.Payments.Events;

/// <summary>Published when a dispute is opened. Admin notification.</summary>
public sealed record DisputeOpenedEto(
    Guid TransactionId, Guid DisputeId, Guid TenantId,
    decimal Amount, string Reason) : IIntegrationEvent;
