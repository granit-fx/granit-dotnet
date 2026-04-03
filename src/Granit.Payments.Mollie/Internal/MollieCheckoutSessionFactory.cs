using Granit.Payments.Contracts;
using Granit.Timing;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models;
using Mollie.Api.Models.Payment.Request;
using Mollie.Api.Models.Payment.Response;

namespace Granit.Payments.Mollie.Internal;

/// <summary>Mollie payment creation (redirects to hosted payment page).</summary>
internal sealed class MollieCheckoutSessionFactory(
    IPaymentClient paymentClient,
    IClock clock) : ICheckoutSessionFactory
{
    /// <inheritdoc/>
    public string ProviderName => "mollie";

    /// <inheritdoc/>
    public async Task<PaymentCheckoutSession> CreateAsync(
        PaymentCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var mollieRequest = new PaymentRequest
        {
            Amount = new Amount(request.Currency.ToUpperInvariant(), request.Amount),
            Description = $"Payment {request.TransactionId}",
            RedirectUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Method = MolliePaymentMethodTypeMapper.ToMollieMethod(request.MethodType),
            Metadata = request.TransactionId.ToString(),
        };

        PaymentResponse response = await paymentClient
            .CreatePaymentAsync(mollieRequest, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new PaymentCheckoutSession(
            Url: response.Links?.Checkout?.Href ?? string.Empty,
            SessionId: response.Id,
            ExpiresAt: response.ExpiresAt ?? clock.Now.AddMinutes(15));
    }
}
