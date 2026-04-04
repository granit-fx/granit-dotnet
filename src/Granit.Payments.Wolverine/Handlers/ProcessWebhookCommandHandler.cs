using System.Text.Json;
using Granit.Payments.Commands;
using Granit.Payments.Domain;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Processes inbound webhook events from payment providers.
/// Reconciles the provider-reported status with the local <see cref="PaymentTransaction"/>.
/// </summary>
internal static partial class ProcessWebhookCommandHandler
{
    public static async Task HandleAsync(
        ProcessWebhookCommand command,
        IEnumerable<IPaymentProvider> providers,
        IPaymentTransactionReader transactionReader,
        IPaymentTransactionWriter transactionWriter,
        IClock clock,
        ILogger<ProcessWebhookCommand> logger,
        CancellationToken cancellationToken)
    {
        // Resolve the provider transaction ID from the webhook payload
        string? providerTransactionId = ExtractProviderTransactionId(command.Payload, command.ProviderName);

        if (string.IsNullOrEmpty(providerTransactionId))
        {
            Log.NoTransactionId(logger, command.ProviderName, command.ProviderEventId);
            return;
        }

        // Find the local transaction
        PaymentTransaction? transaction = await transactionReader
            .GetByProviderTransactionIdAsync(command.ProviderName, providerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        if (transaction is null)
        {
            Log.TransactionNotFound(logger, command.ProviderName, providerTransactionId);
            return;
        }

        // Get authoritative status from the provider API
        IPaymentProvider? provider = providers
            .FirstOrDefault(p => p.Name.Equals(command.ProviderName, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            Log.ProviderNotFound(logger, command.ProviderName);
            return;
        }

        Contracts.PaymentProviderStatus providerStatus = await provider
            .GetStatusAsync(providerTransactionId, cancellationToken)
            .ConfigureAwait(false);

        // Apply FSM transition based on provider-reported status
        bool changed = providerStatus.Status switch
        {
            PaymentStatus.Succeeded => transaction.MarkSucceeded(providerTransactionId, clock.Now),
            PaymentStatus.Failed => transaction.MarkFailed(command.EventType, null),
            PaymentStatus.Canceled => transaction.Cancel(clock.Now),
            PaymentStatus.Processing => transaction.MarkProcessing(),
            PaymentStatus.RequiresAction => false, // ActionUrl not available from webhook
            _ => false,
        };

        if (changed)
        {
            await transactionWriter.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
            Log.TransactionUpdated(logger, transaction.Id, providerStatus.Status);
        }
        else
        {
            Log.NoStateChange(logger, transaction.Id, transaction.Status, providerStatus.Status);
        }
    }

    private static string? ExtractProviderTransactionId(JsonElement payload, string providerName) =>
        providerName.ToLowerInvariant() switch
        {
            "stripe" => payload.TryGetProperty("data", out JsonElement data)
                && data.TryGetProperty("object", out JsonElement obj)
                && obj.TryGetProperty("id", out JsonElement id)
                    ? id.GetString()
                    : payload.TryGetProperty("id", out JsonElement rootId)
                        ? rootId.GetString()
                        : null,
            "mollie" => payload.TryGetProperty("id", out JsonElement mollieId)
                ? mollieId.GetString()
                : null,
            _ => payload.TryGetProperty("id", out JsonElement genericId)
                ? genericId.GetString()
                : null,
        };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook from {ProviderName} (event {EventId}): could not extract provider transaction ID from payload")]
        public static partial void NoTransactionId(ILogger logger, string providerName, string eventId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Webhook from {ProviderName}: no local transaction found for provider ID {ProviderTransactionId}")]
        public static partial void TransactionNotFound(ILogger logger, string providerName, string providerTransactionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Webhook from {ProviderName}: no payment provider registered")]
        public static partial void ProviderNotFound(ILogger logger, string providerName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Transaction {TransactionId} updated to {Status} via webhook")]
        public static partial void TransactionUpdated(ILogger logger, Guid transactionId, PaymentStatus status);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Transaction {TransactionId}: no state change (current: {CurrentStatus}, provider: {ProviderStatus})")]
        public static partial void NoStateChange(ILogger logger, Guid transactionId, PaymentStatus currentStatus, PaymentStatus providerStatus);
    }
}
