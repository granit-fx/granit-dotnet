namespace Granit.Payments.Commands;

/// <summary>Command to request a refund on a payment transaction.</summary>
public sealed record RequestRefundCommand(
    Guid TransactionId, Guid TenantId, decimal Amount, string? Reason, string IdempotencyKey);
