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

        // POST /validate — validate a tax ID
        // GET /rates — list current rates
        // GET /rates/{countryCode} — rate for a country
        // POST /calculate — ad-hoc tax calculation

        return group;
    }
}
