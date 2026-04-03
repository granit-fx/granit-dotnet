using Granit.Payments.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Payments.Endpoints.Extensions;

/// <summary>Extension methods for registering payment endpoints.</summary>
public static class PaymentsEndpointRouteBuilderExtensions
{
    /// <summary>Maps the payment endpoints.</summary>
    public static RouteGroupBuilder MapGranitPayments(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("payments")
            .WithTags("Payments");

        // Charge, refund, checkout, methods, webhook endpoints will be added
        // Webhook endpoint: POST /api/payments/webhooks/{provider} [AllowAnonymous]

        return group;
    }
}
