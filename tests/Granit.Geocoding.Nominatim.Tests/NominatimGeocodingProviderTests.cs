using System.Diagnostics;
using System.Net;
using System.Text;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Nominatim.Internal;
using Granit.Geocoding.Nominatim.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Nominatim.Tests;

public sealed class NominatimGeocodingProviderTests
{
    private static readonly PostalAddress Brussels =
        new(Street: "Rue de la Loi 16", PostalCode: "1000", Locality: "Brussels", Country: "BE");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolveAsync_SuccessfulResponse_MapsFirstResultToCoordinate()
    {
        const string json = """
            [{"lat":"50.8476","lon":"4.3572","display_name":"Brussels, Belgium"}]
            """;
        NominatimGeocodingProvider sut = CreateProvider(json, out _);

        GeocodingResult? result = await sut.ResolveAsync(Brussels, Ct);

        result.ShouldNotBeNull();
        result.Coordinate.Latitude.ShouldBe(50.8476);
        result.Coordinate.Longitude.ShouldBe(4.3572);
    }

    [Fact]
    public async Task ResolveAsync_HouseNumberMatch_IsRooftopWithParsedComponents()
    {
        const string json = """
            [{"lat":"50.8476","lon":"4.3572","addresstype":"house","place_rank":30,
              "address":{"house_number":"16","postcode":"1000","country_code":"be"}}]
            """;
        NominatimGeocodingProvider sut = CreateProvider(json, out _);

        GeocodingResult result = (await sut.ResolveAsync(Brussels, Ct)).ShouldNotBeNull();

        result.Precision.ShouldBe(GeocodeMatchPrecision.Rooftop);
        result.HouseNumber.ShouldBe("16");
        result.PostalCode.ShouldBe("1000");
        result.CountryCode.ShouldBe("be");
    }

    [Fact]
    public async Task ResolveAsync_LocalityMatch_IsLocalityPrecisionWithoutHouseNumber()
    {
        const string json = """
            [{"lat":"50.8503","lon":"4.3517","addresstype":"city","place_rank":16,"address":{"country_code":"be"}}]
            """;
        NominatimGeocodingProvider sut = CreateProvider(json, out _);

        GeocodingResult result = (await sut.ResolveAsync(Brussels, Ct)).ShouldNotBeNull();

        result.Precision.ShouldBe(GeocodeMatchPrecision.Locality);
        result.HouseNumber.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_EmptyArray_ReturnsNull()
    {
        NominatimGeocodingProvider sut = CreateProvider("[]", out _);

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_UnparseableCoordinates_ReturnsNull()
    {
        NominatimGeocodingProvider sut = CreateProvider("""[{"lat":"north","lon":""}]""", out _);

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_BuildsStructuredQueryFromAddressComponents()
    {
        NominatimGeocodingProvider sut = CreateProvider("[]", out StubHttpMessageHandler handler);

        await sut.ResolveAsync(Brussels, Ct);

        handler.LastRequest.ShouldNotBeNull();
        // AbsoluteUri preserves the percent-encoding (ToString() would decode it for display).
        string uri = handler.LastRequest.RequestUri!.AbsoluteUri;
        uri.ShouldContain("search?");
        uri.ShouldContain("street=Rue%20de%20la%20Loi%2016");
        uri.ShouldContain("postalcode=1000");
        uri.ShouldContain("city=Brussels");
        uri.ShouldContain("country=BE");
        uri.ShouldContain("format=jsonv2");
        uri.ShouldContain("limit=1");
        uri.ShouldContain("addressdetails=1");
    }

    [Fact]
    public async Task ResolveAsync_OmitsAbsentOptionalComponents()
    {
        NominatimGeocodingProvider sut = CreateProvider("[]", out StubHttpMessageHandler handler);

        await sut.ResolveAsync(new PostalAddress(Street: null, PostalCode: null, "Brussels", "BE"), Ct);

        string uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        uri.ShouldNotContain("street=");
        uri.ShouldNotContain("postalcode=");
        uri.ShouldContain("city=Brussels");
    }

    [Fact]
    public async Task ResolveAsync_NonSuccessStatus_ReturnsNull()
    {
        NominatimGeocodingProvider sut = CreateProvider(
            new StubHttpMessageHandler("[]", HttpStatusCode.TooManyRequests));

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_TransportError_ReturnsNullWithoutThrowing()
    {
        NominatimGeocodingProvider sut = CreateProvider(
            new ThrowingHttpMessageHandler(new HttpRequestException("network down")));

        (await sut.ResolveAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_SecondCall_IsThrottledToTheConfiguredRate()
    {
        // rate = 10/s -> the second request must wait ~100ms for its slot. Lower-bound assertion only, so a slow
        // CI box can never make this flaky.
        NominatimGeocodingProvider sut = CreateProvider("[]", out _, rateLimitPerSecond: 10);

        await sut.ResolveAsync(Brussels, Ct); // first request: no wait, books the next slot

        long start = Stopwatch.GetTimestamp();
        await sut.ResolveAsync(Brussels, Ct); // second request: paced
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
    }

    private static NominatimGeocodingProvider CreateProvider(
        string json,
        out StubHttpMessageHandler handler,
        double rateLimitPerSecond = 1000)
    {
        handler = new StubHttpMessageHandler(json, HttpStatusCode.OK);
        return BuildProvider(handler, rateLimitPerSecond);
    }

    private static NominatimGeocodingProvider CreateProvider(HttpMessageHandler handler) =>
        BuildProvider(handler, rateLimitPerSecond: 1000);

    private static NominatimGeocodingProvider BuildProvider(HttpMessageHandler handler, double rateLimitPerSecond)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("https://nominatim.openstreetmap.org") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(NominatimGeocodingProvider.HttpClientName).Returns(client);

        NominatimGeocodingOptions options = new()
        {
            UserAgent = "Granit.Tests/1.0 (test@example.com)",
            RateLimitPerSecond = rateLimitPerSecond,
        };
        return new NominatimGeocodingProvider(
            factory,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<NominatimGeocodingProvider>.Instance,
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
