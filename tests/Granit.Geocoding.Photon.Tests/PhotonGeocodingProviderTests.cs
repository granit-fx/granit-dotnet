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
