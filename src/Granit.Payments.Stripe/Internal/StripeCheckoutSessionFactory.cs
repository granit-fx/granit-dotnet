using Granit.Payments.Contracts;
using Stripe;
using Stripe.Checkout;

namespace Granit.Payments.Stripe.Internal;

/// <summary>Stripe Checkout Session implementation using hosted payment pages.</summary>
internal sealed class StripeCheckoutSessionFactory(
    IStripeClient stripeClient) : ICheckoutSessionFactory
{
    /// <inheritdoc/>
    public string ProviderName => "stripe";

    /// <inheritdoc/>
    public async Task<PaymentCheckoutSession> CreateAsync(
        PaymentCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = StripePaymentMethodTypeMapper.ToStripeTypes(request.MethodType),
            LineItems =
            [
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = StripeAmountConverter.ToStripeAmount(request.Amount, request.Currency),
                        Currency = request.Currency.ToLowerInvariant(),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Payment {request.TransactionId}",
                        },
                    },
                    Quantity = 1,
                },
            ],
            Mode = "payment",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            ClientReferenceId = request.TransactionId.ToString(),
        };

        var service = new SessionService(stripeClient);
        Session session = await service.CreateAsync(options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new PaymentCheckoutSession(
            Url: session.Url,
            SessionId: session.Id,
            ExpiresAt: new DateTimeOffset(session.ExpiresAt, TimeSpan.Zero));
    }
}
