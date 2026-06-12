using System.Net;
using System.Text;
using Granit.IpGeolocation.IpInfo.Internal;
using Granit.IpGeolocation.IpInfo.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.IpInfo.Tests;

public sealed class IpInfoIpGeolocationProviderTests
{
    private const string PublicIp = "8.8.8.8";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolveAsync_SuccessfulResponse_MapsFields()
    {
        const string json = """
            {"ip":"8.8.8.8","city":"Mountain View","region":"California","country":"US","loc":"37.4056,-122.0775"}
            """;
        IpInfoIpGeolocationProvider sut = CreateProvider(JsonResponse(json), out _);

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
        IpInfoIpGeolocationProvider sut = CreateProvider(
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
        IpInfoIpGeolocationProvider sut = CreateProvider(
            new HttpResponseMessage(HttpStatusCode.NotFound), out _);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_BogonResponse_ReturnsNull()
    {
        IpInfoIpGeolocationProvider sut = CreateProvider(
            JsonResponse("""{"ip":"8.8.8.8","bogon":true}"""), out _);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_EmptyPayload_ReturnsNull()
    {
        IpInfoIpGeolocationProvider sut = CreateProvider(JsonResponse("{}"), out _);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("//attacker.example")]
    [InlineData("8.8.8.8/../../admin")]
    public async Task ResolveAsync_NonIpInput_ReturnsNullWithoutCallingHttp(string input)
    {
        IpInfoIpGeolocationProvider sut = CreateProvider(JsonResponse("""{"country":"US"}"""), out StubHttpMessageHandler handler);

        (await sut.ResolveAsync(input, Ct)).ShouldBeNull();
        handler.LastRequest.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_TransportError_ReturnsNullWithoutThrowing()
    {
        IpInfoIpGeolocationProvider sut = CreateProvider(
            new ThrowingHttpMessageHandler(new HttpRequestException("network down")));

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ResponseExceedsBufferCap_ReturnsNullWithoutThrowing()
    {
        // The HttpClient is registered with MaxResponseContentBufferSize = MaxResponseSizeBytes; a body larger
        // than the cap makes the content read throw, which the provider swallows to null. Locks in the
        // "bounds memory even on a hostile/oversized response" guarantee the option documents.
        string oversized = $$"""{"country":"US","pad":"{{new string('x', 4096)}}"}""";
        StubHttpMessageHandler handler = new(JsonResponse(oversized));
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("https://ipinfo.io"),
            MaxResponseContentBufferSize = 256,
        };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(IpInfoIpGeolocationProvider.HttpClientName).Returns(client);
        IpInfoIpGeolocationProvider sut = new(
            factory,
            Microsoft.Extensions.Options.Options.Create(new IpInfoIpGeolocationOptions()),
            NullLogger<IpInfoIpGeolocationProvider>.Instance);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static IpInfoIpGeolocationProvider CreateProvider(HttpResponseMessage response, out StubHttpMessageHandler handler, string? token = null)
    {
        handler = new StubHttpMessageHandler(response);
        return BuildProvider(handler, token);
    }

    private static IpInfoIpGeolocationProvider CreateProvider(HttpMessageHandler handler) =>
        BuildProvider(handler, token: null);

    private static IpInfoIpGeolocationProvider BuildProvider(HttpMessageHandler handler, string? token)
    {
        HttpClient client = new(handler) { BaseAddress = new Uri("https://ipinfo.io") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(IpInfoIpGeolocationProvider.HttpClientName).Returns(client);

        IpInfoIpGeolocationOptions options = new() { ApiToken = token };
        return new IpInfoIpGeolocationProvider(
            factory,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<IpInfoIpGeolocationProvider>.Instance);
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
