using System.Collections.Immutable;
using Granit.Guids;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Payments.SepaDirectDebit.Builtin.Internal;

/// <summary>
/// IPaymentProvider bridge for SEPA Direct Debit. Creates collections in Pending status.
/// Reconciliation (CAMT.053 import) updates the status to Succeeded/Failed.
/// </summary>
internal sealed partial class SepaDirectDebitPaymentProvider(
    IGuidGenerator guidGenerator,
    ILogger<SepaDirectDebitPaymentProvider> logger) : IPaymentProvider
{
    /// <inheritdoc/>
    public string Name => "sepa-direct-debit";

    /// <inheritdoc/>
    public IReadOnlyList<PaymentMethodDescriptor> SupportedMethods { get; } =
    [
        new(PaymentMethods.SepaDebit, PaymentMethodCategory.BankDebit),
    ];

    /// <inheritdoc/>
    public Task<IReadOnlyList<PaymentMethodCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PaymentMethodCatalogEntry> catalog =
        [
            new(
                MethodType: PaymentMethods.SepaDebit,
                Category: PaymentMethodCategory.BankDebit,
                DisplayLabel: "SEPA Direct Debit",
                Capability: new PaymentMethodCapability(
                    SupportedCountries: PaymentMethodCountries.SepaZone,
                    SupportedCurrencies: PaymentMethodCurrencies.EurOnly,
                    SupportedSequenceTypes: PaymentMethodSequenceType.First | PaymentMethodSequenceType.Recurring,
                    AmountBounds: ImmutableDictionary<string, PaymentMethodAmountBound>.Empty)),
        ];
        return Task.FromResult(catalog);
    }

    /// <inheritdoc/>
    public Task<PaymentProviderChargeResult> ChargeAsync(
        PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        string collectionId = $"sdd-{guidGenerator.Create():N}";

        Log.CollectionInitiated(logger, request.TransactionId, request.Amount);

        return Task.FromResult(new PaymentProviderChargeResult(
            ProviderTransactionId: collectionId,
            Status: ProviderChargeStatus.Processing,
            ActionUrl: null));
    }

    /// <inheritdoc/>
    public Task<PaymentProviderRefundResult> RefundAsync(
        PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        Log.RefundRequested(logger, request.ProviderTransactionId, request.Amount);

        return Task.FromResult(new PaymentProviderRefundResult(
            ProviderRefundId: $"sdd-refund-{guidGenerator.Create():N}",
            Status: RefundStatus.Pending));
    }

    /// <inheritdoc/>
    public Task<PaymentProviderStatus> GetStatusAsync(
        string providerTransactionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentProviderStatus(
            providerTransactionId, PaymentStatus.Processing));
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "SDD collection initiated: {TransactionId}, {Amount}")]
        public static partial void CollectionInitiated(ILogger logger, Guid transactionId, decimal amount);

        [LoggerMessage(Level = LogLevel.Information, Message = "SDD refund requested: {TransactionId}, {Amount}")]
        public static partial void RefundRequested(ILogger logger, string transactionId, decimal amount);
    }
}
