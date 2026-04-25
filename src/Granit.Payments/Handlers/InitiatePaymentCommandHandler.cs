using System.Diagnostics.CodeAnalysis;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Payments.Commands;
using Granit.Payments.Contracts;
using Granit.Payments.Diagnostics;
using Granit.Payments.Domain;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.Handlers;

/// <summary>
/// Processes <see cref="InitiatePaymentCommand"/> — resolves the payment provider,
/// creates a <see cref="PaymentTransaction"/> aggregate, charges via the provider,
/// and applies the resulting FSM transition.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed partial class InitiatePaymentCommandHandler
{
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Wolverine handler injects DI services per-message; no natural domain wrapper for these orthogonal collaborators (resolver, providers, writer, guid, clock, metrics, tenant, logger).")]
    public static async Task HandleAsync(
        InitiatePaymentCommand command,
        IPaymentProviderResolver providerResolver,
        IEnumerable<IPaymentProvider> providers,
        IPaymentTransactionWriter transactionWriter,
        IGuidGenerator guidGenerator,
        IClock clock,
        PaymentsMetrics metrics,
        ICurrentTenant currentTenant,
        ILogger<InitiatePaymentCommandHandler> logger,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(command.TenantId))
        {
            IPaymentProvider provider = command.ProviderName is not null
                ? providers.First(p => p.Name.Equals(command.ProviderName, StringComparison.OrdinalIgnoreCase))
                : await providerResolver.ResolveAsync(command.TenantId, command.MethodType, cancellationToken)
                    .ConfigureAwait(false);

            var transaction = PaymentTransaction.Create(
                guidGenerator.Create(),
                command.TenantId,
                command.InvoiceId,
                command.Amount,
                command.Currency,
                provider.Name,
                command.MethodType,
                command.IdempotencyKey);

            PaymentProviderChargeResult chargeResult;
            try
            {
                chargeResult = await provider.ChargeAsync(
                    new PaymentChargeRequest(
                        transaction.Id,
                        command.Amount,
                        command.Currency,
                        command.IdempotencyKey),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                transaction.MarkFailed("provider_error", ex.Message);
                await transactionWriter.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
                metrics.RecordFailed(command.TenantId.ToString(), provider.Name, "provider_error");
                Log.ChargeFailed(logger, transaction.Id, provider.Name, ex.Message);
                return;
            }

            switch (chargeResult.Status)
            {
                case ProviderChargeStatus.Succeeded:
                    transaction.MarkSucceeded(chargeResult.ProviderTransactionId, clock.Now);
                    metrics.RecordSucceeded(command.TenantId.ToString(), provider.Name);
                    break;

                case ProviderChargeStatus.RequiresAction:
                    transaction.MarkRequiresAction(chargeResult.ActionUrl ?? string.Empty);
                    break;

                case ProviderChargeStatus.Processing:
                    transaction.MarkProcessing();
                    break;

                case ProviderChargeStatus.Failed:
                    transaction.MarkFailed("charge_declined", null);
                    metrics.RecordFailed(command.TenantId.ToString(), provider.Name, "charge_declined");
                    break;
            }

            await transactionWriter.AddAsync(transaction, cancellationToken).ConfigureAwait(false);
            metrics.RecordCreated(command.TenantId.ToString(), provider.Name);
            Log.PaymentInitiated(logger, transaction.Id, provider.Name, chargeResult.Status);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Payment {TransactionId} initiated via {ProviderName}: {Status}")]
        public static partial void PaymentInitiated(ILogger logger, Guid transactionId, string providerName, ProviderChargeStatus status);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Payment {TransactionId} charge failed via {ProviderName}: {Error}")]
        public static partial void ChargeFailed(ILogger logger, Guid transactionId, string providerName, string error);
    }
}
