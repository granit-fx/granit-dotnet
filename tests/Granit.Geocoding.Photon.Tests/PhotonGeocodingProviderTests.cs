using System.Diagnostics;
using System.Net;
using System.Text;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Photon.Internal;
using Granit.Geocoding.Photon.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Photon.Tests;

public sealed class PhotonGeocodingProviderTests
{
    private static readonly PostalAddress Brussels =
        new(Street: "Rue de la Loi 16", PostalCode: "1000", Locality: "Brussels", Country: "BE");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // GeoJSON orders coordinates [longitude, latitude].
    private const string BrusselsFeature =
        """{"type":"FeatureCollection","features":[{"geometry":{"type":"Point","coordinates":[4.3572,50.8476]}}]}""";

    [Fact]
    public async Task ResolveAsync_SuccessfulResponse_MapsFirstFeatureToCoordinate()
    {
        PhotonGeocodingProvider sut = CreateProvider(BrusselsFeature, out _);

        GeocodingResult? result = await sut.ResolveAsync(Brussels, Ct);

        result.ShouldNotBeNull();
        result.Coordinate.Latitude.ShouldBe(50.8476);
        result.Coordinate.Longitude.ShouldBe(4.3572);
    }

    [Fact]
    public async Task ResolveAsync_HouseFeature_IsRooftopWithParsedComponents()
    {
        const string json = """
            {"features":[{"geometry":{"coordinates":[4.3572,50.8476]},
              "properties":{"type":"house","housenumber":"16","postcode":"1000","countrycode":"BE"}}]}
            """;
        PhotonGeocodingProvider sut = CreateProvider(json, out _);

        GeocodingResult result = (await sut.ResolveAsync(Brussels, Ct)).ShouldNotBeNull();

        result.Precision.ShouldBe(GeocodeMatchPrecision.Rooftop);
        result.HouseNumber.ShouldBe("16");
        result.PostalCode.ShouldBe("1000");
        result.CountryCode.ShouldBe("BE");
    }

    [Fact]
    public async Task ResolveAsync_CityFeature_IsLocalityPrecision()
    {
        const string json = """
            {"features":[{"geometry":{"coordinates":[4.3517,50.8503]},"properties":{"type":"city","countrycode":"BE"}}]}
            """;
        PhotonGeocodingProvider sut = CreateProvider(json, out _);

        GeocodingResult result = (await sut.ResolveAsync(Brussels, Ct)).ShouldNotBeNull();

        result.Precision.ShouldBe(GeocodeMatchPrecision.Locality);
        result.HouseNumber.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_EmptyFeatureCollection_ReturnsNull()
    {
        PhotonGeocodingProvider sut = CreateProvider("""{"type":"FeatureCollection","features":[]}""", out _);

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_FeatureWithoutCoordinates_ReturnsNull()
    {
        PhotonGeocodingProvider sut = CreateProvider(
            """{"features":[{"geometry":{"type":"Point","coordinates":[]}}]}""", out _);

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_OutOfRangeCoordinate_ReturnsNull()
    {
        PhotonGeocodingProvider sut = CreateProvider(
            """{"features":[{"geometry":{"coordinates":[999,999]}}]}""", out _);

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_BuildsFreeTextQueryFromAddressComponents()
    {
        PhotonGeocodingProvider sut =
            CreateProvider("""{"features":[]}""", out StubHttpMessageHandler handler);

        await sut.ResolveAsync(Brussels, Ct);

        handler.LastRequest.ShouldNotBeNull();
        // AbsoluteUri preserves the percent-encoding (ToString() would decode it for display).
        string uri = handler.LastRequest.RequestUri!.AbsoluteUri;
        uri.ShouldContain("api?");
        uri.ShouldContain("q=");
        uri.ShouldContain("Rue%20de%20la%20Loi%2016");
        uri.ShouldContain("Brussels");
        uri.ShouldContain("BE");
        uri.ShouldContain("limit=1");
    }

    [Fact]
    public async Task ResolveAsync_OmitsAbsentOptionalComponents()
    {
        PhotonGeocodingProvider sut =
            CreateProvider("""{"features":[]}""", out StubHttpMessageHandler handler);

        await sut.ResolveAsync(new PostalAddress(Street: null, PostalCode: null, "Brussels", "BE"), Ct);

        string uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        // "Rue de la Loi" and the postal code are absent, so only the locality/country reach the query.
        uri.ShouldContain("Brussels");
        uri.ShouldContain("BE");
        uri.ShouldNotContain("1000");
    }

    [Fact]
    public async Task ResolveAsync_PassesConfiguredLanguage()
    {
        PhotonGeocodingProvider sut =
            CreateProvider("""{"features":[]}""", out StubHttpMessageHandler handler, language: "fr");

        await sut.ResolveAsync(Brussels, Ct);

        handler.LastRequest!.RequestUri!.AbsoluteUri.ShouldContain("lang=fr");
    }

    [Fact]
    public async Task ResolveAsync_NonSuccessStatus_ReturnsNull()
    {
        PhotonGeocodingProvider sut = CreateProvider(
            new StubHttpMessageHandler("""{"features":[]}""", HttpStatusCode.TooManyRequests));

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_TransportError_ReturnsNullWithoutThrowing()
    {
        PhotonGeocodingProvider sut = CreateProvider(
            new ThrowingHttpMessageHandler(new HttpRequestException("network down")));

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_SecondCall_IsThrottledToTheConfiguredRate()
    {
        // rate = 10/s -> the second request must wait ~100ms for its slot. Lower-bound assertion only, so a slow
        // CI box can never make this flaky.
        PhotonGeocodingProvider sut = CreateProvider("""{"features":[]}""", out _, rateLimitPerSecond: 10);

        await sut.ResolveAsync(Brussels, Ct); // first request: no wait, books the next slot

        long start = Stopwatch.GetTimestamp();
        await sut.ResolveAsync(Brussels, Ct); // second request: paced
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
    }

    [Fact]
    public async Task ReverseAsync_SuccessfulResponse_MapsToPostalAddress()
    {
        const string json = """
            {"features":[{"geometry":{"coordinates":[4.3572,50.8476]},
              "properties":{"type":"house","housenumber":"16","street":"Rue de la Loi","postcode":"1000","city":"Brussels","countrycode":"BE"}}]}
            """;
        PhotonGeocodingProvider sut = CreateProvider(json, out _);

        ReverseGeocodingResult result = (await sut.ReverseAsync(new GeoCoordinate(50.8476, 4.3572), Ct)).ShouldNotBeNull();

        result.Address.Locality.ShouldBe("Brussels");
        result.Address.Country.ShouldBe("BE");
        result.Address.Street.ShouldBe("Rue de la Loi 16");
        result.Address.PostalCode.ShouldBe("1000");
        result.Precision.ShouldBe(GeocodeMatchPrecision.Rooftop);
    }

    [Fact]
    public async Task ReverseAsync_WithoutCity_ReturnsNull()
    {
        PhotonGeocodingProvider sut = CreateProvider("""{"features":[{"properties":{"countrycode":"BE"}}]}""", out _);

        (await sut.ReverseAsync(new GeoCoordinate(0, 0), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ReverseAsync_BuildsReverseQueryFromCoordinate()
    {
        PhotonGeocodingProvider sut = CreateProvider("""{"features":[]}""", out StubHttpMessageHandler handler);

        await sut.ReverseAsync(new GeoCoordinate(50.8476, 4.3572), Ct);

        string uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        uri.ShouldContain("reverse?");
        uri.ShouldContain("lat=50.8476");
        uri.ShouldContain("lon=4.3572");
    }

    [Fact]
    public async Task SuggestAsync_ParsesFeaturesToSuggestions()
    {
        const string json = """
            {"features":[
              {"geometry":{"coordinates":[4.3572,50.8476]},
               "properties":{"type":"house","street":"Rue de la Loi","housenumber":"16","postcode":"1000","city":"Brussels","countrycode":"BE"}},
              {"geometry":{"coordinates":[4.35,50.85]},
               "properties":{"type":"city","name":"Brussels","city":"Brussels","countrycode":"BE"}}]}
            """;
        PhotonGeocodingProvider sut = CreateProvider(json, out _);

        IReadOnlyList<AddressSuggestion> suggestions = await sut.SuggestAsync("rue de la loi", 5, Ct);

        suggestions.Count.ShouldBe(2);
        suggestions[0].Label.ShouldBe("Rue de la Loi 16, 1000 Brussels, BE");
        suggestions[0].Address.Locality.ShouldBe("Brussels");
        suggestions[0].Address.Country.ShouldBe("BE");
        suggestions[0].Coordinate.ShouldBe(new GeoCoordinate(50.8476, 4.3572));
        suggestions[0].Precision.ShouldBe(GeocodeMatchPrecision.Rooftop);
    }

    [Fact]
    public async Task SuggestAsync_EmptyFeatureCollection_ReturnsEmpty()
    {
        PhotonGeocodingProvider sut = CreateProvider("""{"features":[]}""", out _);

        (await sut.SuggestAsync("brussels", 5, Ct)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SuggestAsync_BlankQuery_Throws(string query)
    {
        PhotonGeocodingProvider sut = CreateProvider("""{"features":[]}""", out _);

        await Should.ThrowAsync<ArgumentException>(async () => await sut.SuggestAsync(query, 5, Ct));
    }

    [Fact]
    public async Task SuggestAsync_BuildsQueryWithLimit()
    {
        PhotonGeocodingProvider sut = CreateProvider("""{"features":[]}""", out StubHttpMessageHandler handler);

        await sut.SuggestAsync("rue de la loi", 7, Ct);

        string uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        uri.ShouldContain("api?");
        uri.ShouldContain("q=");
        uri.ShouldContain("limit=7");
    }

    private static PhotonGeocodingProvider CreateProvider(
        string json,
        out StubHttpMessageHandler handler,
        double rateLimitPerSecond = 1000,
        string? language = null)
    {
        handler = new StubHttpMessageHandler(json, HttpStatusCode.OK);
        return BuildProvider(handler, rateLimitPerSecond, language);
    }

    private static PhotonGeocodingProvider CreateProvider(HttpMessageHandler handler) =>
        BuildProvider(handler, rateLimitPerSecond: 1000, language: null);

    private static PhotonGeocodingProvider BuildProvider(
        HttpMessageHandler handler, double rateLimitPerSecond, string? language)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("https://photon.komoot.io") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(PhotonGeocodingProvider.HttpClientName).Returns(client);

        PhotonGeocodingOptions options = new()
        {
            RateLimitPerSecond = rateLimitPerSecond,
            Language = language,
        };
        return new PhotonGeocodingProvider(
            factory,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<PhotonGeocodingProvider>.Instance,
            TimeProvider.System);
    }

    private sealed class StubHttpMessageHandler(string json, HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            HttpResponseMessage response = new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
