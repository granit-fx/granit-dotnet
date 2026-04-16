using Granit.Payments.Endpoints.Endpoints;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Payments.Endpoints.Extensions;

/// <summary>Extension methods for registering payment endpoints.</summary>
public static class PaymentsEndpointRouteBuilderExtensions
{
    /// <summary>Maps the payment endpoints.</summary>
    /// <remarks>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitPayments();
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitPayments(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("payments")
            .WithTags("Payments");

        group.MapTransactionEndpoints();
        group.MapPaymentMethodEndpoints();
        group.MapPaymentMethodConfigurationEndpoints();
        group.MapWebhookEndpoints();

        return group;
    }
}
