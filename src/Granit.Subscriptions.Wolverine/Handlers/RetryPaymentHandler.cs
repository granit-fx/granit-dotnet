using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Subscriptions.Scheduling;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.Wolverine.Handlers;

/// <summary>
/// Handles scheduled payment retries during dunning. Sends a new
/// <see cref="InitiatePaymentCommand"/> with a retry-specific idempotency key.
/// </summary>
internal static partial class RetryPaymentHandler
{
    public static async Task HandleAsync(
        RetryPaymentPayload payload,
        IMessageBus messageBus,
        ICurrentTenant currentTenant,
        ILogger<RetryPaymentPayload> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(payload.TenantId))
        {
            var command = new InitiatePaymentCommand(
                payload.InvoiceId,
                payload.TenantId,
                payload.Amount,
                payload.Currency,
                payload.MethodType,
                $"inv-{payload.InvoiceId:N}-retry-{payload.Attempt}",
                payload.ProviderName);

            await messageBus.SendAsync(command).ConfigureAwait(false);
            Log.RetryInitiated(logger, payload.InvoiceId, payload.Attempt);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Payment retry {Attempt} initiated for invoice {InvoiceId}")]
        public static partial void RetryInitiated(ILogger logger, Guid invoiceId, int attempt);
    }
}
