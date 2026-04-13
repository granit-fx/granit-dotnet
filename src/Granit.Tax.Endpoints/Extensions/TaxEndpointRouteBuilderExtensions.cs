using Granit.Tax.Endpoints.Endpoints;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.Tax.Endpoints.Extensions;

/// <summary>Extension methods for registering tax endpoints.</summary>
public static class TaxEndpointRouteBuilderExtensions
{
    /// <summary>Maps the tax administration endpoints.</summary>
    public static RouteGroupBuilder MapGranitTax(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGranitGroup("tax")
            .WithTags("Tax");

        group.MapGroup("tax-ids").MapValidationEndpoints();
        group.MapGroup("tax-rates").MapRateEndpoints();

        return group;
    }
}
