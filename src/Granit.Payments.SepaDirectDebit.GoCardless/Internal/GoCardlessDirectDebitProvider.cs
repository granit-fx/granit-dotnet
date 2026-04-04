using GoCardless;
using GoCardless.Services;
using Granit.Payments.SepaDirectDebit.Contracts;
using Granit.Payments.SepaDirectDebit.Domain;
using Microsoft.Extensions.Logging;
using GcMandateStatus = GoCardless.Resources.MandateStatus;
using GcPaymentStatus = GoCardless.Resources.PaymentStatus;

namespace Granit.Payments.SepaDirectDebit.GoCardless.Internal;

/// <summary>GoCardless implementation of <see cref="IDirectDebitProvider"/>.</summary>
internal sealed partial class GoCardlessDirectDebitProvider(
    GoCardlessClient client,
    ILogger<GoCardlessDirectDebitProvider> logger) : IDirectDebitProvider
{
    /// <inheritdoc/>
    public string Name => "gocardless";

    /// <inheritdoc/>
    public async Task<MandateSetupResult> SetupMandateAsync(
        MandateSetupRequest request, CancellationToken cancellationToken = default)
    {
        RedirectFlowCreateRequest flowRequest = new()
        {
            Description = $"SEPA DD mandate for tenant {request.TenantId}",
            SessionToken = request.TenantId.ToString(),
            SuccessRedirectUrl = request.RedirectUrl ?? "https://localhost/mandate-complete",
        };

        RedirectFlowResponse response = await client.RedirectFlows.CreateAsync(flowRequest)
            .ConfigureAwait(false);

        Log.MandateFlowCreated(logger, response.RedirectFlow.Id);

        return new MandateSetupResult(
            ProviderMandateId: response.RedirectFlow.Id,
            RedirectUrl: response.RedirectFlow.RedirectUrl,
            Status: MandateStatus.Pending);
    }

    /// <inheritdoc/>
    public async Task CancelMandateAsync(
        string providerMandateId, CancellationToken cancellationToken = default)
    {
        await client.Mandates.CancelAsync(providerMandateId).ConfigureAwait(false);
        Log.MandateCancelled(logger, providerMandateId);
    }

    /// <inheritdoc/>
    public async Task<CollectionResult> CollectAsync(
        CollectionRequest request, CancellationToken cancellationToken = default)
    {
        PaymentCreateRequest paymentRequest = new()
        {
            Amount = checked((int)Math.Round(request.Amount * 100m, 0, MidpointRounding.AwayFromZero)),
            Currency = PaymentCreateRequest.PaymentCurrency.EUR,
            Description = $"Invoice {request.InvoiceId}",
            ChargeDate = request.RequestedDate.ToString("yyyy-MM-dd"),
            Links = new PaymentCreateRequest.PaymentLinks { Mandate = request.ProviderMandateId },
            Metadata = new Dictionary<string, string> { ["invoice_id"] = request.InvoiceId.ToString() },
        };

        PaymentResponse response = await client.Payments.CreateAsync(paymentRequest).ConfigureAwait(false);

        Log.CollectionCreated(logger, response.Payment.Id, request.Amount);

        return new CollectionResult(
            ProviderCollectionId: response.Payment.Id,
            Status: MapPaymentStatus(response.Payment.Status));
    }

    /// <inheritdoc/>
    public async Task<CollectionStatusResult> GetCollectionStatusAsync(
        string providerCollectionId, CancellationToken cancellationToken = default)
    {
        PaymentResponse response = await client.Payments.GetAsync(providerCollectionId).ConfigureAwait(false);

        return new CollectionStatusResult(
            Status: MapPaymentStatus(response.Payment.Status),
            FailureCode: null,
            FailureReason: null);
    }

    private static CollectionStatus MapPaymentStatus(GcPaymentStatus? status) => status switch
    {
        GcPaymentStatus.PendingSubmission or GcPaymentStatus.PendingCustomerApproval => CollectionStatus.Pending,
        GcPaymentStatus.Submitted => CollectionStatus.Submitted,
        GcPaymentStatus.Confirmed => CollectionStatus.Processing,
        GcPaymentStatus.PaidOut => CollectionStatus.Succeeded,
        GcPaymentStatus.Failed or GcPaymentStatus.Cancelled or GcPaymentStatus.ChargedBack => CollectionStatus.Failed,
        GcPaymentStatus.CustomerApprovalDenied => CollectionStatus.Failed,
        _ => CollectionStatus.Pending,
    };

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "GoCardless mandate flow created: {FlowId}")]
        public static partial void MandateFlowCreated(ILogger logger, string flowId);

        [LoggerMessage(Level = LogLevel.Information, Message = "GoCardless mandate cancelled: {MandateId}")]
        public static partial void MandateCancelled(ILogger logger, string mandateId);

        [LoggerMessage(Level = LogLevel.Information, Message = "GoCardless collection created: {PaymentId}, {Amount}")]
        public static partial void CollectionCreated(ILogger logger, string paymentId, decimal amount);
    }
}
