using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Tax.Endpoints.Endpoints;
using Granit.Tax.Endpoints.Permissions;
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

        group.MapGranitGroup("ids").MapValidationEndpoints();

        // Rate endpoints — query engine for list/meta/saved-views, custom lookup for /{countryCode}.
        // Both behind Tax.Rates.Read.
        RouteGroupBuilder ratesGroup = group
            .MapGranitGroup("rates")
            .RequireAuthorization(TaxPermissions.Rates.Read);
        ratesGroup.MapGranitQuery<TaxRateEntry>();
        ratesGroup.MapRateEndpoints();

        return group;
    }
}
