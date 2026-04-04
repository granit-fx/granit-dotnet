using Granit.Payments.Commands;
using Granit.Subscriptions.Scheduling;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Subscriptions.Wolverine.Services;

/// <summary>
/// Dispatches payment retry commands during dunning.
/// </summary>
public sealed partial class PaymentRetryDispatcher(
    IMessageBus messageBus,
    ILogger<PaymentRetryDispatcher> logger)
{
    public async Task DispatchRetryAsync(RetryPaymentPayload payload)
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

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Payment retry {Attempt} initiated for invoice {InvoiceId}")]
        public static partial void RetryInitiated(ILogger logger, Guid invoiceId, int attempt);
    }
}
