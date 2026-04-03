using Granit.Payments.Stripe.Options;
using Microsoft.Extensions.Options;
using Stripe;

namespace Granit.Payments.Stripe.Internal;

/// <summary>
/// Creates <see cref="IStripeClient"/> instances per request using <see cref="IHttpClientFactory"/>.
/// </summary>
/// <remarks>
/// Never uses <see cref="StripeConfiguration.ApiKey"/> (global static, unsafe for multi-tenant).
/// Each client is isolated with its own API key and HTTP client from the pool.
/// </remarks>
internal sealed class StripeClientFactory(
    IOptions<StripeOptions> options,
    IHttpClientFactory httpClientFactory)
{
    /// <summary>Creates a new <see cref="IStripeClient"/> with the configured API key.</summary>
    public IStripeClient Create()
    {
        HttpClient httpClient = httpClientFactory.CreateClient("Stripe");
        return new StripeClient(
            apiKey: options.Value.SecretKey,
            httpClient: new SystemNetHttpClient(httpClient));
    }
}
