using Granit.Guids;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.SepaTransfer.Internal;

/// <summary>
/// SEPA bank transfer payment provider. Zero external dependency.
/// </summary>
/// <remarks>
/// <para>Charge creates a transaction in <c>Processing</c> state (awaiting bank transfer).
/// The transaction moves to <c>Succeeded</c> when the reconciliation processor matches
/// a bank statement entry.</para>
/// <para>Refunds for bank transfers are manual (operator issues a reverse transfer).</para>
/// </remarks>
internal sealed partial class SepaTransferPaymentProvider(
    IGuidGenerator guidGenerator,
    ILogger<SepaTransferPaymentProvider> logger) : IPaymentProvider
{
    /// <inheritdoc/>
    public string Name => "sepa-transfer";

    /// <inheritdoc/>
    public IReadOnlyList<PaymentMethodDescriptor> SupportedMethods { get; } =
    [
        new(PaymentMethods.BankTransfer, PaymentMethodCategory.BankTransfer),
    ];

    /// <inheritdoc/>
    public Task<PaymentProviderChargeResult> ChargeAsync(
        PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        // SEPA transfers are always async — the customer must initiate the bank transfer.
        // We return Processing status immediately. The reconciliation processor will
        // mark it as Succeeded when the transfer is matched.

        Log.TransferInitiated(logger, request.TransactionId, request.Amount, request.Currency);

        return Task.FromResult(new PaymentProviderChargeResult(
            ProviderTransactionId: $"sepa-{request.TransactionId:N}",
            Status: ProviderChargeStatus.Processing,
            ActionUrl: null));
    }

    /// <inheritdoc/>
    public Task<PaymentProviderRefundResult> RefundAsync(
        PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        // Bank transfer refunds are manual — the operator initiates a reverse transfer.
        // We mark the refund as Pending and log it.

        Log.RefundRequested(logger, request.ProviderTransactionId, request.Amount);

        return Task.FromResult(new PaymentProviderRefundResult(
            ProviderRefundId: $"sepa-refund-{guidGenerator.Create():N}",
            Status: RefundStatus.Pending));
    }

    /// <inheritdoc/>
    public Task<PaymentProviderStatus> GetStatusAsync(
        string providerTransactionId, CancellationToken cancellationToken = default)
    {
        // Status is determined by the reconciliation processor, not by an external API.
        // Return Processing for all SEPA transfers (until reconciled).

        return Task.FromResult(new PaymentProviderStatus(
            providerTransactionId, PaymentStatus.Processing));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "SEPA transfer initiated: {TransactionId}, {Amount} {Currency}")]
        public static partial void TransferInitiated(ILogger logger, Guid transactionId, decimal amount, string currency);

        [LoggerMessage(Level = LogLevel.Information, Message = "SEPA transfer refund requested: {TransactionId}, {Amount}")]
        public static partial void RefundRequested(ILogger logger, string transactionId, decimal amount);
    }
}
