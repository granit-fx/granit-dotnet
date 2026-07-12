using System.Net;
using Granit.Notifications.MobilePush.GoogleFcm.Internal;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.GoogleFcm.Tests;

public sealed class GoogleFcmAuthenticationHandlerTests
{
    private static readonly DateTimeOffset Anchor = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendAsync_AttachesBearerToken()
    {
        SequencedTokenSource source = new();
        RecordingInnerHandler inner = new(HttpStatusCode.OK);
        using HttpClient client = BuildClient(source, inner);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("https://fcm.googleapis.com/v1/projects/p/messages:send"),
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.AuthorizationHeaders.ShouldBe(["Bearer token-1"]);
    }

    [Fact]
    public async Task SendAsync_On401_RefreshesTokenAndRetriesOnce()
    {
        SequencedTokenSource source = new();
        RecordingInnerHandler inner = new(HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        using HttpClient client = BuildClient(source, inner);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("https://fcm.googleapis.com/v1/projects/p/messages:send"),
            new StringContent("{\"message\":{}}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        source.MintCount.ShouldBe(2);
        inner.AuthorizationHeaders.ShouldBe(["Bearer token-1", "Bearer token-2"]);
        inner.Bodies.ShouldBe(["{\"message\":{}}", "{\"message\":{}}"]);
    }

    [Fact]
    public async Task SendAsync_On401Twice_DoesNotRetryASecondTime()
    {
        SequencedTokenSource source = new();
        RecordingInnerHandler inner = new(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized);
        using HttpClient client = BuildClient(source, inner);

        HttpResponseMessage response = await client.PostAsync(
            new Uri("https://fcm.googleapis.com/v1/projects/p/messages:send"),
            new StringContent("{}"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        inner.AuthorizationHeaders.Count.ShouldBe(2);
    }

    private static HttpClient BuildClient(SequencedTokenSource source, RecordingInnerHandler inner)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Anchor);

        GoogleFcmAuthenticationHandler handler = new(
            new GoogleFcmTokenProvider(source, clock),
            NullLogger<GoogleFcmAuthenticationHandler>.Instance)
        {
            InnerHandler = inner,
        };

        return new HttpClient(handler);
    }

    private sealed class SequencedTokenSource : IGoogleFcmTokenSource
    {
        private int _mintCount;

        public int MintCount => Volatile.Read(ref _mintCount);

        public Task<GoogleFcmAccessToken> MintTokenAsync(CancellationToken cancellationToken = default)
        {
            int count = Interlocked.Increment(ref _mintCount);
            return Task.FromResult(new GoogleFcmAccessToken($"token-{count}", Anchor.AddHours(1)));
        }
    }

    private sealed class RecordingInnerHandler(params HttpStatusCode[] statusCodes) : HttpMessageHandler
    {
        private int _callIndex;

        public List<string?> AuthorizationHeaders { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

            HttpStatusCode status = statusCodes[Math.Min(_callIndex++, statusCodes.Length - 1)];
            return new HttpResponseMessage(status);
        }
    }
}
