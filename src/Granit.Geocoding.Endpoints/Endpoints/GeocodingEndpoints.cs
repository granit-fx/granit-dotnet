using Granit.Domain.ValueObjects;
using Granit.Geocoding.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Geocoding.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for geocoding capabilities. Each endpoint is mapped only when the matching
/// capability is present, so the surface reflects the installed providers.
/// </summary>
internal static class GeocodingEndpoints
{
    /// <summary>Maps the capability-gated geocoding endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapGeocodingEndpoints(
        this RouteGroupBuilder group, GeocodingCapabilities capabilities)
    {
        if (capabilities.Autocomplete)
        {
            group.MapGet("/autocomplete", HandleAutocompleteAsync)
                .WithName("GeocodeAutocomplete")
                .WithSummary("Suggests addresses for a partial query (typeahead).")
                .WithDescription("Returns ranked address suggestions for the partial text. Mapped only when an autocomplete-capable provider is registered. High-volume (per-keystroke): the host should apply a per-principal rate-limit policy.")
                .Produces<GeocodingAutocompleteResponse>()
                .ProducesProblem(StatusCodes.Status400BadRequest);
        }

        if (capabilities.Reverse)
        {
            group.MapGet("/reverse", HandleReverseAsync)
                .WithName("GeocodeReverse")
                .WithSummary("Reverse-geocodes a coordinate to the nearest address.")
                .WithDescription("Returns the postal address nearest the given latitude/longitude. Mapped only when a reverse-capable provider is registered.")
                .Produces<GeocodingReverseResponse>()
                .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
                .ProducesProblem(StatusCodes.Status404NotFound);
        }

        return group;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Ok<GeocodingAutocompleteResponse>> HandleAutocompleteAsync(
        [FromQuery] string q,
        [FromServices] IAddressAutocompleteService service,
        CancellationToken cancellationToken,
        [FromQuery] int limit = 5)
    {
        IReadOnlyList<AddressSuggestion> suggestions =
            await service.SuggestAsync(q, limit, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<GeocodingSuggestionResponse> mapped =
        [
            .. suggestions.Select(static s => new GeocodingSuggestionResponse(
                s.Label,
                s.Address.Street,
                s.Address.PostalCode,
                s.Address.Locality,
                s.Address.Country,
                s.Coordinate?.Latitude,
                s.Coordinate?.Longitude)),
        ];

        return TypedResults.Ok(new GeocodingAutocompleteResponse(mapped));
    }

    private static async Task<Results<Ok<GeocodingReverseResponse>, ProblemHttpResult>> HandleReverseAsync(
        [AsParameters] GeocodingReverseRequest request,
        [FromServices] IReverseGeocodingService service,
        CancellationToken cancellationToken)
    {
        // Coordinate bounds are enforced by GeocodingReverseRequestValidator (localized 422), so the
        // value is in range here.
        GeoCoordinate coordinate = new(request.Lat, request.Lon);

        ReverseGeocodingResult? result =
            await service.ReverseAsync(coordinate, cancellationToken).ConfigureAwait(false);
        if (result is null)
        {
            return TypedResults.Problem(
                detail: "No address could be resolved for the given coordinate.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(new GeocodingReverseResponse(
            result.Address.Street,
            result.Address.PostalCode,
            result.Address.Locality,
            result.Address.Country,
            result.Precision));
    }
}
