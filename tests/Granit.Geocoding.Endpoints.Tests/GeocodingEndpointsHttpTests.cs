using System.Net;
using System.Net.Http.Json;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Endpoints.Dtos;
using Granit.Geocoding.Endpoints.Extensions;
using Granit.Testing.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Endpoints.Tests;

public sealed class GeocodingEndpointsHttpTests : IAsyncDisposable
{
    private readonly IAddressAutocompleteService _autocomplete = Substitute.For<IAddressAutocompleteService>();
    private readonly IReverseGeocodingService _reverse = Substitute.For<IReverseGeocodingService>();
    private readonly GranitEndpointTestHost _host;
    private readonly HttpClient _client;
    private readonly HttpClient _anon;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GeocodingEndpointsHttpTests()
    {
        _autocomplete.SuggestAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<AddressSuggestion>>(_ =>
            [
                new AddressSuggestion(
                    "Rue de la Loi 16, 1000 Brussels, BE",
                    new PostalAddress("Rue de la Loi 16", "1000", "Brussels", "BE"),
                    new GeoCoordinate(50.8503, 4.3517),
                    GeocodeMatchPrecision.Rooftop),
            ]);
        _reverse.ReverseAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns(new ReverseGeocodingResult(
                new PostalAddress("Rue de la Loi 16", "1000", "Brussels", "BE"), GeocodeMatchPrecision.Rooftop));

        _host = GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();
                services.AddSingleton(_autocomplete);
                services.AddSingleton(_reverse);
                services.AddSingleton(new GeocodingCapabilities(Forward: true, Autocomplete: true, Reverse: true));
            },
            configureEndpoints: app => app.MapGranitGeocoding())
            .GetAwaiter().GetResult();

        _client = _host.CreateAuthenticatedClient();
        _anon = _host.CreateAnonymousClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        _anon.Dispose();
        await _host.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Autocomplete_Authenticated_ReturnsSuggestions()
    {
        GeocodingAutocompleteResponse? body = await _client.GetFromJsonAsync<GeocodingAutocompleteResponse>(
            "/geocoding/autocomplete?q=rue&limit=5", Ct);

        body.ShouldNotBeNull();
        body.Suggestions.ShouldHaveSingleItem().Locality.ShouldBe("Brussels");
    }

    [Fact]
    public async Task Autocomplete_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _anon.GetAsync("/geocoding/autocomplete?q=rue", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reverse_ValidCoordinate_ReturnsAddress()
    {
        GeocodingReverseResponse? body = await _client.GetFromJsonAsync<GeocodingReverseResponse>(
            "/geocoding/reverse?lat=50.85&lon=4.35", Ct);

        body.ShouldNotBeNull();
        body.Locality.ShouldBe("Brussels");
        body.Precision.ShouldBe("Rooftop");
    }

    [Fact]
    public async Task Reverse_OutOfRangeCoordinate_Returns400()
    {
        HttpResponseMessage response = await _client.GetAsync("/geocoding/reverse?lat=999&lon=4.35", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reverse_NoMatch_Returns404()
    {
        _reverse.ReverseAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns((ReverseGeocodingResult?)null);

        HttpResponseMessage response = await _client.GetAsync("/geocoding/reverse?lat=0&lon=0", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Autocomplete_NotMappedWhenCapabilityAbsent_Returns404()
    {
        await using GranitEndpointTestHost host = await GranitEndpointTestHost.StartAsync(
            configureServices: services =>
            {
                services.AddAuthorizationBuilder();
                services.AddSingleton(Substitute.For<IAddressAutocompleteService>());
                services.AddSingleton(Substitute.For<IReverseGeocodingService>());
                services.AddSingleton(new GeocodingCapabilities(Forward: true, Autocomplete: false, Reverse: true));
            },
            configureEndpoints: app => app.MapGranitGeocoding(),
            cancellationToken: Ct);
        using HttpClient client = host.CreateAuthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/geocoding/autocomplete?q=rue", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
