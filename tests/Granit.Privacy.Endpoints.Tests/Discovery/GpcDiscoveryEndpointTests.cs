using System.Net;
using System.Text.Json;
using Granit.Privacy.Endpoints.Discovery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Discovery;

public sealed class GpcDiscoveryEndpointTests
{
    private const string DiscoveryPath = "/.well-known/gpc.json";

    [Fact]
    public async Task Disabled_ReturnsNotFound()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using TestApp app = await TestApp.CreateAsync(
            new GpcDiscoveryOptions { Enabled = false },
            requireAuthFallback: false,
            ct).ConfigureAwait(true);

        HttpResponseMessage response = await app.Client.GetAsync(DiscoveryPath, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Enabled_ReturnsSpecCompliantDocument()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        DateOnly lastUpdate = new(2026, 5, 1);

        await using TestApp app = await TestApp.CreateAsync(new GpcDiscoveryOptions
        {
            Enabled = true,
            LastUpdate = lastUpdate,
        }, requireAuthFallback: false, ct).ConfigureAwait(true);

        HttpResponseMessage response = await app.Client.GetAsync(DiscoveryPath, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        // Use a JsonDocument so we assert the wire shape, not C# field naming.
        Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(true);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(true);
        JsonElement root = doc.RootElement;

        root.GetProperty("gpc").GetBoolean().ShouldBeTrue();
        root.GetProperty("lastUpdate").GetString().ShouldBe("2026-05-01");
    }

    [Fact]
    public async Task Enabled_EmitsCacheControlPublicMaxAge()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using TestApp app = await TestApp.CreateAsync(new GpcDiscoveryOptions
        {
            Enabled = true,
            LastUpdate = new DateOnly(2026, 5, 1),
            CacheMaxAgeSeconds = 3_600,
        }, requireAuthFallback: false, ct).ConfigureAwait(true);

        HttpResponseMessage response = await app.Client.GetAsync(DiscoveryPath, ct).ConfigureAwait(true);

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(3_600));
    }

    [Fact]
    public async Task Enabled_AllowsAnonymous_DespiteAuthFallback()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Sets a global RequireAuthenticatedUser fallback so any endpoint
        // without AllowAnonymous would be challenged — proves the discovery
        // endpoint correctly opts out of auth.
        await using TestApp app = await TestApp.CreateAsync(new GpcDiscoveryOptions
        {
            Enabled = true,
            LastUpdate = new DateOnly(2026, 5, 1),
        }, requireAuthFallback: true, ct).ConfigureAwait(true);

        HttpResponseMessage response = await app.Client.GetAsync(DiscoveryPath, ct).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class TestApp : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public HttpClient Client { get; }

        private TestApp(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public static async Task<TestApp> CreateAsync(
            GpcDiscoveryOptions options,
            bool requireAuthFallback,
            CancellationToken cancellationToken)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            builder.Services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, AlwaysFailAuthHandler>("Test", _ => { });

            AuthorizationBuilder authBuilder = builder.Services.AddAuthorizationBuilder();
            if (requireAuthFallback)
            {
                authBuilder.SetFallbackPolicy(new AuthorizationPolicyBuilder("Test")
                    .RequireAuthenticatedUser()
                    .Build());
            }

            builder.Services.AddSingleton<IOptions<GpcDiscoveryOptions>>(
                Microsoft.Extensions.Options.Options.Create(options));

            WebApplication app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGranitPrivacyGpcDiscovery();
            await app.StartAsync(cancellationToken).ConfigureAwait(false);

            return new TestApp(app, app.GetTestClient());
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class AlwaysFailAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }
}
