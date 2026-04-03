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

        return group;
    }
}
