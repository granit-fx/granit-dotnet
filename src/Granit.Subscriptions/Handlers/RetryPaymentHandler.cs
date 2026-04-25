using System.Diagnostics.CodeAnalysis;
using Granit.Commands;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Subscriptions.Scheduling;
using Microsoft.Extensions.Logging;

namespace Granit.Subscriptions.Handlers;

/// <summary>
/// Handles scheduled payment retries during dunning — builds an
/// <see cref="InitiatePaymentCommand"/> from the retry payload and sends it via
/// <see cref="ICommandSender"/>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed partial class RetryPaymentHandler
{
    public static async Task HandleAsync(
        RetryPaymentPayload payload,
        ICommandSender commandSender,
        ICurrentTenant currentTenant,
        ILogger<RetryPaymentHandler> logger,
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

            await commandSender.SendAsync(command, cancellationToken).ConfigureAwait(false);
            Log.RetryInitiated(logger, payload.InvoiceId, payload.Attempt);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Payment retry {Attempt} initiated for invoice {InvoiceId}")]
        public static partial void RetryInitiated(ILogger logger, Guid invoiceId, int attempt);
    }
}
