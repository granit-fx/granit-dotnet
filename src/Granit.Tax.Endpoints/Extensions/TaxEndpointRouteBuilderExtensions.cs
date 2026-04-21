using Granit.Authorization.Extensions;
using Granit.QueryEngine.AspNetCore.Extensions;
using Granit.Tax.Endpoints.Endpoints;
using Granit.Tax.Endpoints.Options;
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
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="TaxEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitTax(
        this IEndpointRouteBuilder endpoints,
        Action<TaxEndpointsOptions>? configure = null)
    {
        TaxEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        group.MapGranitGroup("ids")
            .WithTags(options.ValidationTagName)
            .MapValidationEndpoints();

        // Rate endpoints — query engine for list/meta/saved-views, custom lookup for /{countryCode}.
        // Both behind Tax.Rates.Read.
        RouteGroupBuilder ratesGroup = group
            .MapGranitGroup("rates")
            .WithTags(options.RatesTagName)
            .RequireAuthorization(TaxPermissions.Rates.Read)
            .AllowHostAccess();
        ratesGroup.MapGranitQuery<TaxRateEntry>();
        ratesGroup.MapRateEndpoints();

        return group;
    }
}
