using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Bff.Diagnostics;
using Granit.Bff.Endpoints.Extensions;
using Granit.Bff.Endpoints.Internal;
using Granit.Bff.Options;
using Granit.Http.Cookies;
using Granit.Oidc.DPoP;
using Granit.Timing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Bff.Endpoints.Tests.Integration;

/// <summary>
/// Test server that boots a minimal ASP.NET Core application with mocked services
/// and the BFF endpoints registered via <see cref="BffEndpointRouteBuilderExtensions.MapGranitBff"/>.
/// </summary>
internal sealed class BffEndpointsTestServer : IAsyncDisposable
{
    public static readonly DateTimeOffset FixedNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The test frontend name used across all BFF integration tests.
    /// </summary>
    internal const string TestFrontendName = "test-app";

    /// <summary>
    /// The path prefix for the test frontend. Endpoints are under <c>/app/bff/...</c>.
    /// </summary>
    internal const string TestPathPrefix = "/app";

    /// <summary>
    /// The session cookie name used by the test frontend.
    /// Computed from <see cref="BffFrontendOptions.SessionCookieName"/>: <c>__Host-bff-{Name}</c>.
    /// </summary>
    internal const string TestSessionCookieName = "__Host-bff-test-app";

    private const string AuthenticatedRole = "authenticated";

    private readonly WebApplication _app;
    private readonly HttpMessageHandler _testHandler;

    public HttpClient AuthenticatedClient { get; }
    public HttpClient AnonymousClient { get; }

    /// <summary>Application services, for inspecting mapped endpoints and their metadata.</summary>
    public IServiceProvider Services => _app.Services;

    public IBffTokenStore TokenStore { get; }
    public IBffCsrfTokenGenerator CsrfGenerator { get; }
    public ILogoutTokenValidator LogoutTokenValidator { get; }
    public IGranitCookieManager CookieManager { get; }
    public ICookieRegistry CookieRegistry { get; }
    public IDPoPProofService DPoPService { get; }
    public IClock Clock { get; }
    public IFusionCache Cache { get; }
    public MockTokenEndpointHandler TokenEndpointHandler { get; }

    private BffEndpointsTestServer(
        WebApplication app,
        HttpMessageHandler testHandler,
        HttpClient authenticatedClient,
        HttpClient anonymousClient,
        IBffTokenStore tokenStore,
        IBffCsrfTokenGenerator csrfGenerator,
        ILogoutTokenValidator logoutTokenValidator,
        IGranitCookieManager cookieManager,
        ICookieRegistry cookieRegistry,
        IDPoPProofService dpopService,
        IClock clock,
        IFusionCache cache,
        MockTokenEndpointHandler tokenEndpointHandler)
    {
        _app = app;
        _testHandler = testHandler;
        AuthenticatedClient = authenticatedClient;
        AnonymousClient = anonymousClient;
        TokenStore = tokenStore;
        CsrfGenerator = csrfGenerator;
        LogoutTokenValidator = logoutTokenValidator;
        CookieManager = cookieManager;
        CookieRegistry = cookieRegistry;
        DPoPService = dpopService;
        Clock = clock;
        Cache = cache;
        TokenEndpointHandler = tokenEndpointHandler;
    }

    public static async Task<BffEndpointsTestServer> CreateAsync(string[]? scopes = null)
    {
        // Reset the static singleton guard so each test can register endpoints
        ResetEndpointsMappedFlag();

        IBffTokenStore tokenStore = Substitute.For<IBffTokenStore>();
        IBffCsrfTokenGenerator csrfGenerator = Substitute.For<IBffCsrfTokenGenerator>();
        ILogoutTokenValidator logoutTokenValidator = Substitute.For<ILogoutTokenValidator>();
        IGranitCookieManager cookieManager = Substitute.For<IGranitCookieManager>();
        ICookieRegistry cookieRegistry = Substitute.For<ICookieRegistry>();
        IDPoPProofService dpopService = Substitute.For<IDPoPProofService>();
        IClock clock = Substitute.For<IClock>();
        IFusionCache cache = Substitute.For<IFusionCache>();

        clock.Now.Returns(FixedNow);

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // Authentication
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder();

        // Service mocks
        builder.Services.AddSingleton(tokenStore);
        builder.Services.AddSingleton(csrfGenerator);
        builder.Services.AddSingleton(logoutTokenValidator);
        builder.Services.AddSingleton(cookieManager);
        builder.Services.AddSingleton(cookieRegistry);
        builder.Services.AddSingleton(dpopService);
        builder.Services.AddSingleton(clock);
        builder.Services.AddSingleton(cache);

        // Session-enrichment defaults (normally provided by Granit.IpGeolocation / Granit.UserSessions modules).
        builder.Services.AddSingleton(Substitute.For<Granit.IpGeolocation.IIpGeolocationResolver>());
        Granit.UserSessions.IUserSessionRiskStore riskStore = Substitute.For<Granit.UserSessions.IUserSessionRiskStore>();
        riskStore.GetManyAsync(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, Granit.UserSessions.UserSessionRiskVerdict>());
        builder.Services.AddSingleton(riskStore);

        // Options: configure a test frontend under /app
        BffFrontendOptions testFrontend = new()
        {
            Name = TestFrontendName,
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            PathPrefix = TestPathPrefix,
            Scopes = scopes ?? ["openid", "profile"],
        };

        GranitBffOptions bffOptions = new()
        {
            Authority = new Uri("https://auth.example.com"),
            Frontends = [testFrontend],
            RequireIssuerValidation = false,
        };

        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(bffOptions));

        // Real metrics (needs a real IMeterFactory)
        builder.Services.AddMetrics();
        builder.Services.AddSingleton<BffMetrics>();

        // Logout orchestrator (extracted from handler in domain service refactoring)
        builder.Services.AddScoped<IBffLogoutOrchestrator, DefaultBffLogoutOrchestrator>();

        // HttpClientFactory with mock handler that intercepts token/PAR endpoint calls
        MockTokenEndpointHandler tokenEndpointHandler = new();
        builder.Services.AddHttpClient("Granit.Bff")
            .ConfigurePrimaryHttpMessageHandler(() => tokenEndpointHandler);
        builder.Services.AddHttpClient(); // default client for other uses

        WebApplication app = builder.Build();
        app.MapGranitBff();
        await app.StartAsync().ConfigureAwait(false);

        TestServer testServer = app.GetTestServer();
        HttpMessageHandler testHandler = testServer.CreateHandler();

        HttpClient authenticatedClient = testServer.CreateClient();
        authenticatedClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AuthenticatedRole);

        HttpClient anonymousClient = testServer.CreateClient();

        return new BffEndpointsTestServer(
            app, testHandler, authenticatedClient, anonymousClient,
            tokenStore, csrfGenerator, logoutTokenValidator,
            cookieManager, cookieRegistry, dpopService, clock, cache,
            tokenEndpointHandler);
    }

    /// <summary>
    /// Sends an HTTP request without following redirects.
    /// Useful for testing endpoints that return 302 (e.g., logout).
    /// </summary>
    public async Task<HttpResponseMessage> SendWithoutRedirectAsync(
        HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        // The TestServer handler processes the request through the ASP.NET Core pipeline
        // and returns the raw response without following redirects.
        using HttpClient client = new(_testHandler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost"),
        };
        return await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the static <c>s_bffEndpointsMapped</c> flag in
    /// <see cref="BffEndpointRouteBuilderExtensions"/> so each test can create
    /// a fresh server with endpoints mapped.
    /// </summary>
    private static void ResetEndpointsMappedFlag()
    {
        FieldInfo? field = typeof(BffEndpointRouteBuilderExtensions)
            .GetField("s_bffEndpointsMapped", BindingFlags.Static | BindingFlags.NonPublic);
        field?.SetValue(null, false);
    }

    public async ValueTask DisposeAsync()
    {
        AuthenticatedClient.Dispose();
        AnonymousClient.Dispose();
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Mock HTTP handler that intercepts requests to OIDC endpoints (token, PAR).
/// Configure <see cref="TokenResponse"/> to control what the token endpoint returns.
/// </summary>
internal sealed class MockTokenEndpointHandler : HttpMessageHandler
{
    /// <summary>
    /// The JSON response body returned by the mock token endpoint.
    /// Defaults to a standard successful token response.
    /// </summary>
    public string TokenResponse { get; set; } = """
        {
            "access_token": "mock-access-token",
            "refresh_token": "mock-refresh-token",
            "id_token": "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJ1c2VyLTQyIiwibmFtZSI6IkphbmUgRG9lIiwiZW1haWwiOiJqYW5lQGV4YW1wbGUuY29tIn0.",
            "token_type": "Bearer",
            "expires_in": 3600
        }
        """;

    /// <summary>
    /// The HTTP status code returned by the mock token endpoint.
    /// </summary>
    public HttpStatusCode TokenStatusCode { get; set; } = HttpStatusCode.OK;

    /// <summary>
    /// The last request received by the mock handler, for verification.
    /// </summary>
    public HttpRequestMessage? LastRequest { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;

        HttpResponseMessage response = new(TokenStatusCode)
        {
            Content = new StringContent(TokenResponse, System.Text.Encoding.UTF8, "application/json"),
        };

        return Task.FromResult(response);
    }
}

/// <summary>
/// Fake authentication handler that authenticates when the <c>X-Test-Roles</c> header
/// is present. The authenticated user has fixed claims suitable for BFF testing.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Claim[] claims =
        [
            new("sub", "test-user-id"),
            new(ClaimTypes.NameIdentifier, "test-user-id"),
            new(ClaimTypes.Name, "test-user"),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
