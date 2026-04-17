using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.Extensions.Logging;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models;
using Mollie.Api.Models.Payment.Request;
using Mollie.Api.Models.Payment.Response;
using Mollie.Api.Models.Refund.Request;
using Mollie.Api.Models.Refund.Response;
using RefundStatus = Granit.Payments.Domain.RefundStatus;

namespace Granit.Payments.Mollie.Internal;

/// <summary>Mollie implementation of <see cref="IPaymentProvider"/> using the Payments API.</summary>
internal sealed partial class MolliePaymentProvider(
    IPaymentClient paymentClient,
    IRefundClient refundClient,
    ILogger<MolliePaymentProvider> logger) : IPaymentProvider
{
    /// <inheritdoc/>
    public string Name => "mollie";

    /// <inheritdoc/>
    /// <remarks>
    /// Covers Mollie's European surface. Mollie's strength over Stripe is direct
    /// support for Belgian local methods (Belfius, KBC) and vouchers (Paysafecard).
    /// </remarks>
    public IReadOnlyList<PaymentMethodDescriptor> SupportedMethods { get; } =
    [
        // Cards
        new(PaymentMethods.Card, PaymentMethodCategory.Card),

        // Bank redirects (European local payment methods)
        new(PaymentMethods.Bancontact, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Ideal, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Eps, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Giropay, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Przelewy24, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Twint, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Trustly, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.MyBank, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Belfius, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Kbc, PaymentMethodCategory.BankRedirect),

        // Bank transfer & direct debit
        new(PaymentMethods.BankTransfer, PaymentMethodCategory.BankTransfer),
        new(PaymentMethods.SepaDebit, PaymentMethodCategory.BankDebit),

        // Wallets
        new(PaymentMethods.ApplePay, PaymentMethodCategory.Wallet),
        new(PaymentMethods.PayPal, PaymentMethodCategory.Wallet),

        // Buy now, pay later
        new(PaymentMethods.Klarna, PaymentMethodCategory.BuyNowPayLater),
        new(PaymentMethods.Riverty, PaymentMethodCategory.BuyNowPayLater),

        // Vouchers
        new(PaymentMethods.Paysafecard, PaymentMethodCategory.Voucher),
    ];

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a static catalog curated from the Mollie documentation
    /// (<see cref="MollieCatalog"/>). A future enhancement can call
    /// <c>IMethodClient.GetMethodListAsync(include: "pricing")</c> to pick up
    /// account-level toggles and up-to-date bounds.
    /// </remarks>
    public Task<IReadOnlyList<PaymentMethodCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(MollieCatalog.Entries);

    /// <inheritdoc/>
    public async Task<PaymentProviderChargeResult> ChargeAsync(
        PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var mollieRequest = new PaymentRequest
            {
                Amount = new Amount(request.Currency.ToUpperInvariant(), request.Amount),
                Description = $"Payment {request.TransactionId}",
                RedirectUrl = request.ReturnUrl,
                Metadata = request.TransactionId.ToString(),
            };

            PaymentResponse response = await paymentClient
                .CreatePaymentAsync(mollieRequest, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            ProviderChargeStatus status = MollieStatusMapper.MapChargeStatus(response.Status);
            string? actionUrl = response.Links?.Checkout?.Href;

            Log.ChargeCompleted(logger, response.Id, status);

            return new PaymentProviderChargeResult(response.Id, status, actionUrl);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.ChargeError(logger, ex);
            return new PaymentProviderChargeResult(string.Empty, ProviderChargeStatus.Failed);
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderRefundResult> RefundAsync(
        PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            PaymentResponse payment = await paymentClient
                .GetPaymentAsync(request.ProviderTransactionId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var refundRequest = new RefundRequest
            {
                Amount = new Amount(payment.Amount.Currency, request.Amount),
            };

            RefundResponse response = await refundClient
                .CreatePaymentRefundAsync(request.ProviderTransactionId, refundRequest, cancellationToken)
                .ConfigureAwait(false);

            RefundStatus status = MollieStatusMapper.MapRefundStatus(response.Status);
            Log.RefundCompleted(logger, response.Id, status);

            return new PaymentProviderRefundResult(response.Id, status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.RefundError(logger, ex);
            return new PaymentProviderRefundResult(string.Empty, RefundStatus.Failed);
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderStatus> GetStatusAsync(
        string providerTransactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            PaymentResponse response = await paymentClient
                .GetPaymentAsync(providerTransactionId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            PaymentStatus status = MollieStatusMapper.MapPaymentStatus(response.Status);
            return new PaymentProviderStatus(response.Id, status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.StatusError(logger, ex, providerTransactionId);
            return new PaymentProviderStatus(providerTransactionId, PaymentStatus.Failed);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Mollie charge completed: {PaymentId} -> {Status}")]
        public static partial void ChargeCompleted(ILogger logger, string paymentId, ProviderChargeStatus status);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Mollie charge error")]
        public static partial void ChargeError(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Information, Message = "Mollie refund completed: {RefundId} -> {Status}")]
        public static partial void RefundCompleted(ILogger logger, string refundId, RefundStatus status);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Mollie refund error")]
        public static partial void RefundError(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Mollie status check error for {TransactionId}")]
        public static partial void StatusError(ILogger logger, Exception ex, string transactionId);
    }
}
