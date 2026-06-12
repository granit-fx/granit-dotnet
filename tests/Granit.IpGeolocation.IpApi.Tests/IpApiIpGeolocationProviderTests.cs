using System.Net;
using System.Text;
using Granit.IpGeolocation.IpApi.Internal;
using Granit.IpGeolocation.IpApi.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.IpApi.Tests;

public sealed class IpApiIpGeolocationProviderTests
{
    private const string PublicIp = "8.8.8.8";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolveAsync_SuccessfulResponse_MapsFields()
    {
        const string json = """
            {"ip":"8.8.8.8","city":"Mountain View","region":"California","country":"US","loc":"37.4056,-122.0775"}
            """;
        IpApiIpGeolocationProvider sut = CreateProvider(JsonResponse(json), out _);

        GeoLocation? result = await sut.ResolveAsync(PublicIp, Ct);

        result.ShouldNotBeNull();
        result.City.ShouldBe("Mountain View");
        result.Region.ShouldBe("California");
        result.CountryCode.ShouldBe("US");
        result.Country.ShouldBeNull();
        result.Latitude.ShouldBe(37.4056);
        result.Longitude.ShouldBe(-122.0775);
    }

    [Fact]
    public async Task ResolveAsync_WithToken_SendsBearerAuthorizationHeader()
    {
        IpApiIpGeolocationProvider sut = CreateProvider(
            JsonResponse("""{"country":"US"}"""),
            out StubHttpMessageHandler handler,
            token: "secret-token");

        await sut.ResolveAsync(PublicIp, Ct);

        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Headers.Authorization.ShouldNotBeNull();
        handler.LastRequest.Headers.Authorization.Scheme.ShouldBe("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.ShouldBe("secret-token");
    }

    [Fact]
    public async Task ResolveAsync_NonSuccessStatus_ReturnsNull()
    {
        IpApiIpGeolocationProvider sut = CreateProvider(
            new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_BogonResponse_ReturnsNull()
    {
        IpApiIpGeolocationProvider sut = CreateProvider(
            JsonResponse("""{"ip":"8.8.8.8","bogon":true}"""), out _);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_EmptyPayload_ReturnsNull()
    {
        IpApiIpGeolocationProvider sut = CreateProvider(JsonResponse("{}"), out _);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("//attacker.example")]
    [InlineData("8.8.8.8/../../admin")]
    public async Task ResolveAsync_NonIpInput_ReturnsNullWithoutCallingHttp(string input)
    {
        IpApiIpGeolocationProvider sut = CreateProvider(JsonResponse("""{"country":"US"}"""), out StubHttpMessageHandler handler);

        (await sut.ResolveAsync(input, Ct)).ShouldBeNull();
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_TransportError_ReturnsNullWithoutThrowing()
    {
        IpApiIpGeolocationProvider sut = CreateProvider(
            new ThrowingHttpMessageHandler(new HttpRequestException("network down")));

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static IpApiIpGeolocationProvider CreateProvider(HttpResponseMessage response, out StubHttpMessageHandler handler, string? token = null)
    {
        handler = new StubHttpMessageHandler(response);
        return BuildProvider(handler, token);
    }

    private static IpApiIpGeolocationProvider CreateProvider(HttpMessageHandler handler) =>
        BuildProvider(handler, token: null);

    private static IpApiIpGeolocationProvider BuildProvider(HttpMessageHandler handler, string? token)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("https://ipinfo.io") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(IpApiIpGeolocationProvider.HttpClientName).Returns(client);

        IpApiIpGeolocationOptions options = new() { ApiToken = token };
        return new IpApiIpGeolocationProvider(
            factory,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<IpApiIpGeolocationProvider>.Instance);
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }
}
