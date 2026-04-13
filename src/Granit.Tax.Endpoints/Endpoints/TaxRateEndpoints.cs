using Granit.Tax.Endpoints.Dtos;
using Granit.Tax.Endpoints.Permissions;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Tax.Endpoints.Endpoints;

/// <summary>Endpoints for tax rate lookup.</summary>
internal static class TaxRateEndpoints
{
    internal static RouteGroupBuilder MapRateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetAllRatesAsync)
            .RequireAuthorization(TaxPermissions.Rates.Read)
            .WithName("GetAllTaxRates")
            .WithSummary("Returns all currently effective tax rates.")
            .WithDescription(
                "Lists the standard and reduced tax rates for all configured countries. "
                + "Rates come from the configured provider (EU VAT defaults, configuration overrides, "
                + "or database-managed overrides when the EF Core package is registered).")
            .Produces<IReadOnlyList<TaxRateResponse>>();

        group.MapGet("/{countryCode}", GetRateByCountryAsync)
            .RequireAuthorization(TaxPermissions.Rates.Read)
            .WithName("GetTaxRateByCountry")
            .WithSummary("Returns the current tax rate for a specific country.")
            .WithDescription(
                "Returns the effective tax rate for the given ISO 3166-1 alpha-2 country code at the current date. "
                + "Returns 404 if no rate is configured for the country.")
            .Produces<TaxRateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<TaxRateResponse>>> GetAllRatesAsync(
        [FromServices] ITaxRateProvider rateProvider,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TaxRateEntry> rates = await rateProvider
            .GetAllCurrentRatesAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<TaxRateResponse> response = rates
            .Select(r => new TaxRateResponse(
                r.CountryCode, r.StandardRate, r.ReducedRate,
                r.SuperReducedRate, r.ParkingRate,
                r.EffectiveFrom, r.EffectiveTo))
            .ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<TaxRateResponse>, NotFound>> GetRateByCountryAsync(
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
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new TaxRateResponse(
            rate.CountryCode, rate.StandardRate, rate.ReducedRate,
            rate.SuperReducedRate, rate.ParkingRate,
            rate.EffectiveFrom, rate.EffectiveTo));
    }
}
