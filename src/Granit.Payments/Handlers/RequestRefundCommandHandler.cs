using System.Diagnostics.CodeAnalysis;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Payments.Contracts;
using Granit.Payments.Diagnostics;
using Granit.Payments.Domain;
using Granit.Payments.Domain.ValueObjects;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.Handlers;

/// <summary>
/// Processes <see cref="RequestRefundCommand"/> — looks up the transaction,
/// calls the domain's <see cref="PaymentTransaction.RequestRefund"/> method,
/// executes the refund via the payment provider, and persists the result.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed partial class RequestRefundCommandHandler
{
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Wolverine handler injects DI services per-message; no natural domain wrapper for these orthogonal collaborators (reader, writer, providers, guid, clock, metrics, tenant, logger).")]
    public static async Task HandleAsync(
        RequestRefundCommand command,
        IPaymentTransactionReader transactionReader,
        IPaymentTransactionWriter transactionWriter,
        IEnumerable<IPaymentProvider> providers,
        IGuidGenerator guidGenerator,
        IClock clock,
        PaymentsMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<RequestRefundCommandHandler> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(command.TenantId))
        {
            PaymentTransaction? transaction = await transactionReader
                .GetByIdAsync(TransactionId.Create(command.TransactionId), cancellationToken)
                .ConfigureAwait(false);

            if (transaction is null)
            {
                Log.TransactionNotFound(logger, command.TransactionId);
                return;
            }

            if (string.IsNullOrEmpty(transaction.ProviderTransactionId))
            {
                Log.NoProviderTransactionId(logger, command.TransactionId);
                return;
            }

            Refund refund = transaction.RequestRefund(
                guidGenerator.Create(),
                command.Amount,
                clock.Now,
                command.Reason);

            IPaymentProvider? provider = providers
                .FirstOrDefault(p => p.Name.Equals(transaction.ProviderName, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                Log.ProviderNotFound(logger, transaction.ProviderName);
                refund.MarkFailed();
                await transactionWriter.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                PaymentProviderRefundResult result = await provider.RefundAsync(
                    new PaymentRefundRequest(
                        transaction.ProviderTransactionId,
                        command.Amount,
                        command.IdempotencyKey),
                    cancellationToken).ConfigureAwait(false);

                if (result.Status == RefundStatus.Succeeded)
                {
                    transaction.CompleteRefund(refund.Id, result.ProviderRefundId, clock.Now);
                    metrics.RecordRefundCompleted(command.TenantId.ToString());
                    Log.RefundCompleted(logger, refund.Id, command.TransactionId);
                }
                else
                {
                    refund.MarkFailed();
                    Log.RefundFailed(logger, refund.Id, command.TransactionId);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                refund.MarkFailed();
                Log.RefundException(logger, refund.Id, command.TransactionId, ex.Message);
            }

            await transactionWriter.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Refund command: transaction {TransactionId} not found")]
        public static partial void TransactionNotFound(ILogger logger, Guid transactionId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Refund command: transaction {TransactionId} has no provider transaction ID")]
        public static partial void NoProviderTransactionId(ILogger logger, Guid transactionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Refund command: provider {ProviderName} not registered")]
        public static partial void ProviderNotFound(ILogger logger, string providerName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Refund {RefundId} completed for transaction {TransactionId}")]
        public static partial void RefundCompleted(ILogger logger, Guid refundId, Guid transactionId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Refund {RefundId} failed for transaction {TransactionId}")]
        public static partial void RefundFailed(ILogger logger, Guid refundId, Guid transactionId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Refund {RefundId} exception for transaction {TransactionId}: {Error}")]
        public static partial void RefundException(ILogger logger, Guid refundId, Guid transactionId, string error);
    }
}
