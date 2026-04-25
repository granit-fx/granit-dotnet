using Granit.Tax.Endpoints.Dtos;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Tax.Endpoints.Endpoints;

/// <summary>
/// Custom tax rate endpoints. The list endpoint (GET /, /meta, /saved-views/*)
/// is provided by <c>MapGranitQuery&lt;TaxRateEntry&gt;()</c>; only the per-country
/// lookup lives here.
/// </summary>
internal static class TaxRateEndpoints
{
    internal static RouteGroupBuilder MapRateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{countryCode:length(2)}", GetRateByCountryAsync)
            .WithName("GetTaxRateByCountry")
            .WithSummary("Returns the current tax rate for a specific country.")
            .WithDescription(
                "Returns the effective tax rate for the given ISO 3166-1 alpha-2 country code at the current date. "
                + "Returns 404 if no rate is configured for the country.")
            .Produces<TaxRateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<TaxRateResponse>, ProblemHttpResult>> GetRateByCountryAsync(
        string countryCode,
        [FromServices] ITaxRateProvider rateProvider,
        [FromServices] IClock clock,
        CancellationToken cancellationToken = default)
    {
        TaxRateEntry? rate = await rateProvider
            .GetRateAsync(countryCode, clock.Now, cancellationToken)
            .ConfigureAwait(false);

        if (rate is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(new TaxRateResponse(
            rate.CountryCode, rate.StandardRate, rate.ReducedRate,
            rate.SuperReducedRate, rate.ParkingRate,
            rate.EffectiveFrom, rate.EffectiveTo));
    }
}
