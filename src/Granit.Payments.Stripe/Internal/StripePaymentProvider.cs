using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.Extensions.Logging;
using Stripe;
using StripeRefund = Stripe.Refund;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe implementation of <see cref="IPaymentProvider"/> using PaymentIntent API.</summary>
internal sealed partial class StripePaymentProvider(
    IStripeClient stripeClient,
    IPaymentMethodReader paymentMethodReader,
    ILogger<StripePaymentProvider> logger) : IPaymentProvider
{
    /// <inheritdoc/>
    public string Name => "stripe";

    /// <inheritdoc/>
    /// <remarks>
    /// Covers the European surface of Stripe's PaymentIntents API. Stripe-exclusive
    /// methods from other regions (alipay, wechat_pay, us_bank_account, ...) are
    /// omitted because they aren't declared in <see cref="PaymentMethods"/>.
    /// Belgian local methods (Belfius, KBC) and Alma/Paysafecard belong to other
    /// providers — not Stripe.
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
        new(PaymentMethods.Blik, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Twint, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.Trustly, PaymentMethodCategory.BankRedirect),
        new(PaymentMethods.MyBank, PaymentMethodCategory.BankRedirect),

        // Bank transfer & direct debit
        new(PaymentMethods.BankTransfer, PaymentMethodCategory.BankTransfer),
        new(PaymentMethods.SepaDebit, PaymentMethodCategory.BankDebit),

        // Wallets
        new(PaymentMethods.ApplePay, PaymentMethodCategory.Wallet),
        new(PaymentMethods.GooglePay, PaymentMethodCategory.Wallet),
        new(PaymentMethods.PayPal, PaymentMethodCategory.Wallet),
        new(PaymentMethods.Alipay, PaymentMethodCategory.Wallet),
        new(PaymentMethods.WechatPay, PaymentMethodCategory.Wallet),

        // Buy now, pay later
        new(PaymentMethods.Klarna, PaymentMethodCategory.BuyNowPayLater),
        new(PaymentMethods.Riverty, PaymentMethodCategory.BuyNowPayLater),
    ];

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a static catalog curated from Stripe's payment method support matrix
    /// (<see cref="StripeCatalog"/>). A future enhancement can call
    /// <c>PaymentMethodConfigurationService.GetAsync()</c> to honor the merchant's
    /// dashboard toggles dynamically.
    /// </remarks>
    public Task<IReadOnlyList<PaymentMethodCatalogEntry>> GetCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(StripeCatalog.Entries);

    /// <inheritdoc/>
    public async Task<PaymentProviderChargeResult> ChargeAsync(
        PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = StripeAmountConverter.ToStripeAmount(request.Amount, request.Currency),
                Currency = request.Currency.ToLowerInvariant(),
                Confirm = true,
                ReturnUrl = request.ReturnUrl,
            };

            if (request.PaymentMethodId.HasValue)
            {
                Domain.PaymentMethod? method = await paymentMethodReader
                    .GetByIdAsync(request.PaymentMethodId.Value, cancellationToken)
                    .ConfigureAwait(false);

                if (method is not null)
                {
                    options.PaymentMethod = method.ProviderMethodId;
                }
            }

            var service = new PaymentIntentService(stripeClient);
            PaymentIntent intent = await service.CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken).ConfigureAwait(false);

            ProviderChargeStatus status = StripeStatusMapper.MapChargeStatus(intent.Status);
            string? actionUrl = intent.NextAction?.RedirectToUrl?.Url;

            Log.ChargeCompleted(logger, intent.Id, status);

            return new PaymentProviderChargeResult(intent.Id, status, actionUrl);
        }
        catch (StripeException ex)
        {
            Log.ChargeError(logger, ex, ex.StripeError?.Code);
            return new PaymentProviderChargeResult(string.Empty, ProviderChargeStatus.Failed);
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderRefundResult> RefundAsync(
        PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var piService = new PaymentIntentService(stripeClient);
            PaymentIntent intent = await piService.GetAsync(
                request.ProviderTransactionId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var options = new RefundCreateOptions
            {
                PaymentIntent = request.ProviderTransactionId,
                Amount = StripeAmountConverter.ToStripeAmount(request.Amount, intent.Currency),
            };

            var service = new RefundService(stripeClient);
            StripeRefund refund = await service.CreateAsync(
                options,
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken).ConfigureAwait(false);

            RefundStatus status = StripeStatusMapper.MapRefundStatus(refund.Status);
            Log.RefundCompleted(logger, refund.Id, status);

            return new PaymentProviderRefundResult(refund.Id, status);
        }
        catch (StripeException ex)
        {
            Log.RefundError(logger, ex, ex.StripeError?.Code);
            return new PaymentProviderRefundResult(string.Empty, RefundStatus.Failed);
        }
    }

    /// <inheritdoc/>
    public async Task<PaymentProviderStatus> GetStatusAsync(
        string providerTransactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var service = new PaymentIntentService(stripeClient);
            PaymentIntent intent = await service.GetAsync(
                providerTransactionId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            PaymentStatus status = StripeStatusMapper.MapPaymentStatus(intent.Status);
            return new PaymentProviderStatus(intent.Id, status);
        }
        catch (StripeException ex)
        {
            Log.StatusError(logger, ex, providerTransactionId);
            return new PaymentProviderStatus(providerTransactionId, PaymentStatus.Failed);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe charge completed: {PaymentIntentId} -> {Status}")]
        public static partial void ChargeCompleted(ILogger logger, string paymentIntentId, ProviderChargeStatus status);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe charge error: {ErrorCode}")]
        public static partial void ChargeError(ILogger logger, Exception ex, string? errorCode);

        [LoggerMessage(Level = LogLevel.Information, Message = "Stripe refund completed: {RefundId} -> {Status}")]
        public static partial void RefundCompleted(ILogger logger, string refundId, RefundStatus status);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe refund error: {ErrorCode}")]
        public static partial void RefundError(ILogger logger, Exception ex, string? errorCode);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe status check error for {TransactionId}")]
        public static partial void StatusError(ILogger logger, Exception ex, string transactionId);
    }
}
