using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using Granit.Events;
using Granit.Identity.Diagnostics;
using Granit.Identity.Events;
using Granit.Identity.Federated.Keycloak.Internal;
using Granit.Identity.Federated.Keycloak.Options;
using Granit.Identity.Federated.RateLimiting;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class KeycloakIdentityProviderTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly KeycloakAdminOptions _options = new()
    {
        BaseUrl = "https://keycloak.test",
        Realm = "test-realm",
        ClientId = "admin-service",
        ClientSecret = "secret",
    };

    private readonly KeycloakAdminTokenService _tokenService;
    private readonly KeycloakUserTokenExchangeService _tokenExchangeService;
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly IdentityMetrics _metrics;
    private readonly ServiceProvider _metricsServiceProvider;
    private readonly KeycloakIdentityProvider _provider;

    // Separate handler for token exchange responses (Account API user tokens).
    private readonly MockHttpMessageHandler _tokenExchangeHandler = new()
    {
        ResponseBody = """{"access_token":"user-token","expires_in":300}""",
    };

    private readonly ActivityListener _activityListener;

    public KeycloakIdentityProviderTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Identity.Keycloak",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://keycloak.test/") };
        _httpClientFactory.CreateClient("KeycloakAdmin").Returns(_httpClient);

        // Token service returns a fake admin token.
        MockHttpMessageHandler tokenHandler = new()
        {
            ResponseBody = """{"access_token":"fake-token","expires_in":300}""",
        };
        HttpClient tokenClient = new(tokenHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory tokenFactory = Substitute.For<IHttpClientFactory>();
        tokenFactory.CreateClient("KeycloakAdmin").Returns(tokenClient);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _tokenService = new KeycloakAdminTokenService(
            tokenFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            clock,
            NullLogger<KeycloakAdminTokenService>.Instance);

        // Token exchange service uses a dedicated factory that returns the user token.
        HttpClient exchangeClient = new(_tokenExchangeHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory exchangeFactory = Substitute.For<IHttpClientFactory>();
        exchangeFactory.CreateClient("KeycloakAdmin").Returns(exchangeClient);

        ServiceCollection metricsServices = new();
        metricsServices.AddMetrics();
        _metricsServiceProvider = metricsServices.BuildServiceProvider();
        _metrics = new IdentityMetrics(_metricsServiceProvider.GetRequiredService<IMeterFactory>());

        ITokenExchangeRateLimiter rateLimiter = Substitute.For<ITokenExchangeRateLimiter>();
        rateLimiter.CheckAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TokenExchangeRateLimitDecision.Allowed);

        _tokenExchangeService = new KeycloakUserTokenExchangeService(
            exchangeFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            rateLimiter,
            _distributedEventBus,
            TimeProvider.System,
            NullLogger<KeycloakUserTokenExchangeService>.Instance);

        _provider = new KeycloakIdentityProvider(
            _tokenService,
            _tokenExchangeService,
            _httpClientFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _distributedEventBus,
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);
    }

    [Fact]
    public async Task GetRoleMembersAsync_WithUsers_ReturnsIdentityUsers()
    {
        _handler.ResponseBody = """[{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true},{"id":"user-2","username":"bob","email":null,"firstName":null,"lastName":null,"enabled":true}]""";

        IReadOnlyList<IIdentityUser> result = await _provider.GetRoleMembersAsync(
            "editor", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].UserId.ShouldBe("user-1");
        result[0].Username.ShouldBe("alice");
        result[0].Email.ShouldBe("alice@test.com");
        result[1].UserId.ShouldBe("user-2");
        result[1].Username.ShouldBe("bob");
    }

    [Fact]
    public async Task GetRoleMembersAsync_EmptyRole_ReturnsEmptyList()
    {
        _handler.ResponseBody = "[]";

        IReadOnlyList<IIdentityUser> result = await _provider.GetRoleMembersAsync(
            "editor", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRoleMembersAsync_KeycloakError_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IIdentityUser> result = await _provider.GetRoleMembersAsync(
            "editor", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRoleMembersAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetRoleMembersAsync("content-editor", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/roles/content-editor/users");
    }

    [Fact]
    public async Task GetUsersAsync_WithSearch_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = """[{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true}]""";

        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            search: "alice", first: 0, max: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Username.ShouldBe("alice");
        _handler.Requests[0].Url.ShouldContain("search=alice");
        _handler.Requests[0].Url.ShouldContain("first=0");
        _handler.Requests[0].Url.ShouldContain("max=10");
    }

    [Fact]
    public async Task GetUserAsync_ExistingUser_ReturnsUser()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe("user-1");
        result.Username.ShouldBe("alice");
    }

    [Fact]
    public async Task GetUserAsync_KeycloakError_ReturnsNull()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NotFound;
        _handler.ResponseBody = string.Empty;

        IIdentityUser? result = await _provider.GetUserAsync(
            "unknown", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetRolesAsync_ReturnsRoles()
    {
        _handler.ResponseBody = """[{"id":"role-1","name":"editor","description":"Content editor"},{"id":"role-2","name":"admin","description":null}]""";

        IReadOnlyList<IdentityRole> result = await _provider.GetRolesAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("editor");
        result[0].Description.ShouldBe("Content editor");
        result[1].Name.ShouldBe("admin");
    }

    [Fact]
    public async Task GetRolesAsync_KeycloakError_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IdentityRole> result = await _provider.GetRolesAsync(
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // --- Argument guard tests ---

    [Fact]
    public async Task GetUserAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.GetUserAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRoleMembersAsync_NullRoleName_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.GetRoleMembersAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Pagination edge cases ---

    [Fact]
    public async Task GetUsersAsync_NoParams_CallsEndpointWithoutQueryString()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldEndWith("/admin/realms/test-realm/users");
        _handler.Requests[0].Url.ShouldNotContain("?");
    }

    [Fact]
    public async Task GetUsersAsync_WithOnlyFirst_IncludesFirstParam()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUsersAsync(
            first: 20,
            cancellationToken: TestContext.Current.CancellationToken);

        _handler.Requests[0].Url.ShouldContain("first=20");
        _handler.Requests[0].Url.ShouldNotContain("max=");
        _handler.Requests[0].Url.ShouldNotContain("search=");
    }

    [Fact]
    public async Task GetUsersAsync_WithOnlyMax_IncludesMaxParam()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUsersAsync(
            max: 50,
            cancellationToken: TestContext.Current.CancellationToken);

        _handler.Requests[0].Url.ShouldContain("max=50");
        _handler.Requests[0].Url.ShouldNotContain("first=");
        _handler.Requests[0].Url.ShouldNotContain("search=");
    }

    [Fact]
    public async Task GetUsersAsync_WithFirstZero_IncludesFirstParam()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUsersAsync(
            first: 0, max: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        _handler.Requests[0].Url.ShouldContain("first=0");
        _handler.Requests[0].Url.ShouldContain("max=10");
    }

    // --- Bearer token tests ---

    [Fact]
    public async Task GetUsersAsync_SetsAuthorizationHeader()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
    }

    // --- Mapping tests ---

    [Fact]
    public async Task GetUserAsync_MapsAllFieldsCorrectly()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe("user-1");
        result.Username.ShouldBe("alice");
        result.Email.ShouldBe("alice@test.com");
        result.FirstName.ShouldBe("Alice");
        result.LastName.ShouldBe("Doe");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserAsync_DisabledUser_MapsEnabledAsFalse()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"bob","email":"bob@test.com","firstName":"Bob","lastName":"Smith","enabled":false}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task GetRolesAsync_MapsDescriptionCorrectly()
    {
        _handler.ResponseBody = """[{"id":"role-1","name":"viewer","description":null}]""";

        IReadOnlyList<IdentityRole> result = await _provider.GetRolesAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Description.ShouldBeNull();
    }

    [Fact]
    public async Task GetRolesAsync_EmptyResponse_ReturnsEmptyList()
    {
        _handler.ResponseBody = "[]";

        IReadOnlyList<IdentityRole> result = await _provider.GetRolesAsync(
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRoleMembersAsync_CallsCorrectEndpointWithSpaceInRoleName()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetRoleMembersAsync(
            "content editor", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        // Uri.ToString() may decode %20 back to a space, so check for the role name presence.
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/roles/content");
        _handler.Requests[0].Url.ShouldContain("editor/users");
    }

    [Fact]
    public async Task GetUsersAsync_KeycloakUnavailable_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = """{"id":"user-abc","username":"test","email":null,"firstName":null,"lastName":null,"enabled":true}""";

        await _provider.GetUserAsync("user-abc", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-abc");
    }

    [Fact]
    public async Task GetRolesAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetRolesAsync(TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/roles");
    }

    // --- SetUserEnabledAsync tests ---

    [Fact]
    public async Task SetUserEnabledAsync_Enable_SendsPutWithEnabledTrue()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetUserEnabledAsync("user-1", true, TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PUT");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-1");
        _handler.Requests[0].Body.ShouldContain("\"enabled\":true");
    }

    [Fact]
    public async Task SetUserEnabledAsync_Disable_SendsEnabledFalse()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetUserEnabledAsync("user-1", false, TestContext.Current.CancellationToken);

        _handler.Requests[0].Body.ShouldContain("\"enabled\":false");
    }

    [Fact]
    public async Task SetUserEnabledAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.SetUserEnabledAsync(null!, true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetUserEnabledAsync_KeycloakError_PropagatesException()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Forbidden;
        _handler.ResponseBody = string.Empty;

        await Should.ThrowAsync<HttpRequestException>(
            () => _provider.SetUserEnabledAsync("user-1", true, TestContext.Current.CancellationToken));
    }

    // --- IUserSessionProvider.ListAsync tests ---

    [Fact]
    public async Task ListSessionsAsync_WithSessions_ReturnsSessionDescriptors()
    {
        _handler.ResponseBody = """[{"id":"sess-1","ipAddress":"1.2.3.4","start":1700000000000,"lastAccess":1700001000000,"rememberMe":false,"clients":{"client-id":"test-app"}},{"id":"sess-2","ipAddress":"5.6.7.8","start":1700002000000,"lastAccess":1700003000000,"rememberMe":true,"clients":{}}]""";

        IReadOnlyList<UserSessionDescriptor> result = await _provider.ListAsync(
            "user-1", "sess-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].SessionId.ShouldBe("sess-1");
        result[0].UserId.ShouldBe("user-1");
        result[0].IsCurrent.ShouldBeTrue();
        result[0].IpAddress.ShouldBe("1.2.3.4");
        result[0].CreatedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
        result[0].LastAccessedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700001000000));
        result[0].UserAgent.ShouldBeNull();
        result[0].Location.ShouldBeNull();
        result[1].SessionId.ShouldBe("sess-2");
        result[1].IsCurrent.ShouldBeFalse();
    }

    [Fact]
    public async Task ListSessionsAsync_EmptyResponse_ReturnsEmptyList()
    {
        _handler.ResponseBody = "[]";

        IReadOnlyList<UserSessionDescriptor> result = await _provider.ListAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListSessionsAsync_KeycloakError_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<UserSessionDescriptor> result = await _provider.ListAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListSessionsAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.ListAsync("user-abc", null, TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-abc/sessions");
    }

    [Fact]
    public async Task ListSessionsAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.ListAsync(null!, null, TestContext.Current.CancellationToken));
    }

    // --- IUserDeviceProvider.ListAsync tests (admin sessions fallback) ---

    [Fact]
    public async Task ListDevicesAsync_WithoutTokenExchange_UsesAdminSessions()
    {
        _handler.ResponseBody = """[{"id":"sess-1","ipAddress":"1.2.3.4","start":1700000000000,"lastAccess":1700001000000,"rememberMe":false,"clients":{"client-id":"test-app"}}]""";

        IReadOnlyList<UserDevice> result = await _provider.ListAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].DeviceId.ShouldBe("sess-1");
        result[0].Kind.ShouldBe(DeviceKind.Browser);
        result[0].LastSeen.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700001000000));
        result[0].OperatingSystem.ShouldBeNull();
        result[0].Browser.ShouldBeNull();
        result[0].SessionCount.ShouldBe(1);
        result[0].LastLocation.ShouldBeNull();
    }

    [Fact]
    public async Task ListDevicesAsync_WithTokenExchange_CallsAccountApi()
    {
        KeycloakAdminOptions opts = new()
        {
            BaseUrl = "https://keycloak.test",
            Realm = "test-realm",
            ClientId = "admin-service",
            ClientSecret = "secret",
            UseTokenExchangeForDeviceActivity = true,
        };

        // Sequence: call 1 = token exchange POST → user token; call 2 = GET /account/sessions/devices → devices.
        MockSequenceHttpMessageHandler seqHandler = new(
        [
            """{"access_token":"user-token","expires_in":300}""",
            """[{"ipAddress":"9.10.11.12","os":"Windows","osVersion":"10","browser":"Chrome/120.0","device":"Desktop","mobile":false,"current":true,"lastAccess":1700005000000,"sessions":[{"id":"sess-x","ipAddress":"9.10.11.12","start":1700004000000,"lastAccess":1700005000000,"rememberMe":false,"clients":{"app-id":"test-app"}}]}]""",
        ]);
        HttpClient seqClient = new(seqHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory seqFactory = Substitute.For<IHttpClientFactory>();
        seqFactory.CreateClient("KeycloakAdmin").Returns(seqClient);

        ITokenExchangeRateLimiter exchangeRateLimiter = Substitute.For<ITokenExchangeRateLimiter>();
        exchangeRateLimiter.CheckAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(TokenExchangeRateLimitDecision.Allowed);

        KeycloakUserTokenExchangeService exchangeSvc = new(
            seqFactory,
            Microsoft.Extensions.Options.Options.Create(opts),
            exchangeRateLimiter,
            _distributedEventBus,
            TimeProvider.System,
            NullLogger<KeycloakUserTokenExchangeService>.Instance);

        KeycloakIdentityProvider provider = new(
            _tokenService,
            exchangeSvc,
            seqFactory,
            Microsoft.Extensions.Options.Options.Create(opts),
            Substitute.For<IDistributedEventBus>(),
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);

        IReadOnlyList<UserDevice> result = await provider.ListAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].DeviceId.ShouldBe("Windows/Chrome/120.0");
        result[0].Kind.ShouldBe(DeviceKind.Browser);
        result[0].OperatingSystem.ShouldBe("Windows");
        result[0].Browser.ShouldBe("Chrome/120.0");
        result[0].LastSeen.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1700005000000));
        result[0].SessionCount.ShouldBe(1);
        result[0].LastLocation.ShouldBeNull();
    }

    [Fact]
    public async Task ListDevicesAsync_KeycloakError_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<UserDevice> result = await _provider.ListAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListDevicesAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.ListAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListDevicesAsync_AdminSessions_ClientDeclaresDeviceKind_ResolvesIt()
    {
        // Sequence: call 1 = GET sessions (one session bound to client uuid-1);
        //           call 2 = GET /clients/uuid-1 → the client declares granit.device_kind = Tv.
        KeycloakIdentityProvider provider = CreateAdminSequenceProvider(
            """[{"id":"sess-1","ipAddress":"1.2.3.4","start":1700000000000,"lastAccess":1700001000000,"rememberMe":false,"clients":{"uuid-1":"tv-app"}}]""",
            """{"id":"uuid-1","clientId":"tv-app","attributes":{"granit.device_kind":["Tv"]}}""");

        IReadOnlyList<UserDevice> result = await provider.ListAsync("user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Kind.ShouldBe(DeviceKind.Tv);
    }

    [Fact]
    public async Task ListDevicesAsync_AdminSessions_ClientWithoutDeviceKind_DefaultsToBrowser()
    {
        KeycloakIdentityProvider provider = CreateAdminSequenceProvider(
            """[{"id":"sess-1","ipAddress":"1.2.3.4","start":1700000000000,"lastAccess":1700001000000,"rememberMe":false,"clients":{"uuid-1":"web-app"}}]""",
            """{"id":"uuid-1","clientId":"web-app","attributes":{}}""");

        IReadOnlyList<UserDevice> result = await provider.ListAsync("user-1", TestContext.Current.CancellationToken);

        result[0].Kind.ShouldBe(DeviceKind.Browser);
    }

    // Builds a provider whose admin "KeycloakAdmin" client serves the given responses in order (the admin-token
    // service keeps its own separate handler, so it never consumes from this sequence).
    private KeycloakIdentityProvider CreateAdminSequenceProvider(params string[] responses)
    {
        MockSequenceHttpMessageHandler seqHandler = new(responses);
        HttpClient seqClient = new(seqHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory seqFactory = Substitute.For<IHttpClientFactory>();
        seqFactory.CreateClient("KeycloakAdmin").Returns(seqClient);

        return new KeycloakIdentityProvider(
            _tokenService,
            _tokenExchangeService,
            seqFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _distributedEventBus,
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);
    }

    // --- GetPasswordChangedAtAsync tests ---

    [Fact]
    public async Task GetPasswordChangedAtAsync_WithPasswordCredential_ReturnsDate()
    {
        _handler.ResponseBody = """[{"id":"cred-1","type":"password","createdDate":1699000000000},{"id":"cred-2","type":"otp","createdDate":1699100000000}]""";

        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1699000000000));
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_WithoutPasswordCredential_ReturnsNull()
    {
        _handler.ResponseBody = """[{"id":"cred-1","type":"otp","createdDate":1699100000000}]""";

        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_EmptyCredentials_ReturnsNull()
    {
        _handler.ResponseBody = "[]";

        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_KeycloakError_ReturnsNull()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetPasswordChangedAtAsync("user-abc", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-abc/credentials");
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.GetPasswordChangedAtAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Attributes mapping tests ---

    [Fact]
    public async Task GetUserAsync_WithAttributes_MapsAttributes()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true,"attributes":{"license":["MD-12345"],"department":["Cardiology"]}}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Metadata.Count.ShouldBe(2);
        result.Metadata["license"].ShouldBe("MD-12345");
        result.Metadata["department"].ShouldBe("Cardiology");
    }

    [Fact]
    public async Task GetUserAsync_WithMultiValueAttributes_TakesFirstValue()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true,"attributes":{"roles":["admin","user"]}}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Metadata["roles"].ShouldBe("admin");
    }

    [Fact]
    public async Task GetUserAsync_WithoutAttributes_MetadataIsEmpty()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserAsync_WithEmptyAttributes_MetadataIsEmpty()
    {
        _handler.ResponseBody = """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true,"attributes":{}}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUsersAsync_WithAttributes_MapsAttributesForAllUsers()
    {
        _handler.ResponseBody = """[{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true,"attributes":{"dept":["IT"]}},{"id":"user-2","username":"bob","email":null,"firstName":null,"lastName":null,"enabled":true}]""";

        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Metadata["dept"].ShouldBe("IT");
        result[1].Metadata.ShouldBeEmpty();
    }

    // --- Feature 1: GetUserRolesAsync tests ---

    [Fact]
    public async Task GetUserRolesAsync_WithRoles_ReturnsIdentityRoles()
    {
        _handler.ResponseBody = """[{"id":"role-1","name":"editor","description":"Content editor"},{"id":"role-2","name":"viewer","description":null}]""";

        IReadOnlyList<IdentityRole> result = await _provider.GetUserRolesAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("role-1");
        result[0].Name.ShouldBe("editor");
        result[1].Name.ShouldBe("viewer");
    }

    [Fact]
    public async Task GetUserRolesAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUserRolesAsync("user-abc", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-abc/role-mappings/realm");
    }

    [Fact]
    public async Task GetUserRolesAsync_KeycloakError_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IdentityRole> result = await _provider.GetUserRolesAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.GetUserRolesAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 1: AssignRoleAsync tests ---

    [Fact]
    public async Task AssignRoleAsync_SendsPostWithRoleRepresentation()
    {
        _handler.ResponseBody = """{"id":"role-1","name":"editor","description":"Content editor"}""";

        await _provider.AssignRoleAsync("user-1", "editor", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(2);
        _handler.Requests[0].Method.ShouldBe("GET");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/roles/editor");
        _handler.Requests[1].Method.ShouldBe("POST");
        _handler.Requests[1].Url.ShouldContain("/admin/realms/test-realm/users/user-1/role-mappings/realm");
        _handler.Requests[1].Body.ShouldContain("\"id\":\"role-1\"");
    }

    [Fact]
    public async Task AssignRoleAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.AssignRoleAsync(null!, "editor", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AssignRoleAsync_NullRoleName_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.AssignRoleAsync("user-1", null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 1: RemoveRoleAsync tests ---

    [Fact]
    public async Task RemoveRoleAsync_SendsDeleteWithRoleRepresentation()
    {
        _handler.ResponseBody = """{"id":"role-1","name":"editor","description":"Content editor"}""";

        await _provider.RemoveRoleAsync("user-1", "editor", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(2);
        _handler.Requests[0].Method.ShouldBe("GET");
        _handler.Requests[1].Method.ShouldBe("DELETE");
        _handler.Requests[1].Url.ShouldContain("/admin/realms/test-realm/users/user-1/role-mappings/realm");
        _handler.Requests[1].Body.ShouldContain("\"id\":\"role-1\"");
    }

    // --- IUserSessionProvider.RevokeAsync tests ---

    [Fact]
    public async Task RevokeAsync_SendsDeleteToCorrectEndpoint_ReturnsTrue()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        bool revoked = await _provider.RevokeAsync("user-1", "sess-abc", TestContext.Current.CancellationToken);

        revoked.ShouldBeTrue();
        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("DELETE");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/sessions/sess-abc");
    }

    [Fact]
    public async Task RevokeAsync_KeycloakError_PropagatesException()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NotFound;
        _handler.ResponseBody = string.Empty;

        await Should.ThrowAsync<HttpRequestException>(
            () => _provider.RevokeAsync("user-1", "sess-abc", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.RevokeAsync(null!, "sess-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeAsync_NullSessionId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.RevokeAsync("user-1", null!, TestContext.Current.CancellationToken));
    }

    // --- IUserSessionProvider.RevokeOthersAsync tests ---

    [Fact]
    public async Task RevokeOthersAsync_RevokesEverySessionExceptCurrent()
    {
        // Sequence: call 1 = GET sessions; calls 2..n = DELETE each non-current session.
        MockSequenceHttpMessageHandler seqHandler = new(
        [
            """[{"id":"sess-1","ipAddress":"1.2.3.4","start":1700000000000,"lastAccess":1700001000000,"rememberMe":false,"clients":{}},{"id":"sess-2","ipAddress":"5.6.7.8","start":1700002000000,"lastAccess":1700003000000,"rememberMe":false,"clients":{}},{"id":"sess-3","ipAddress":"9.9.9.9","start":1700004000000,"lastAccess":1700005000000,"rememberMe":false,"clients":{}}]""",
            string.Empty,
            string.Empty,
        ]);
        HttpClient seqClient = new(seqHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory seqFactory = Substitute.For<IHttpClientFactory>();
        seqFactory.CreateClient("KeycloakAdmin").Returns(seqClient);

        KeycloakIdentityProvider provider = new(
            _tokenService,
            _tokenExchangeService,
            seqFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _distributedEventBus,
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);

        int revoked = await provider.RevokeOthersAsync("user-1", "sess-1", TestContext.Current.CancellationToken);

        revoked.ShouldBe(2);
        seqHandler.Requests.Count.ShouldBe(3);
        seqHandler.Requests[0].Method.ShouldBe("GET");
        seqHandler.Requests[1].Method.ShouldBe("DELETE");
        seqHandler.Requests[1].Url.ShouldContain("/admin/realms/test-realm/sessions/sess-2");
        seqHandler.Requests[2].Url.ShouldContain("/admin/realms/test-realm/sessions/sess-3");
    }

    [Fact]
    public async Task RevokeOthersAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.RevokeOthersAsync(null!, "sess-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeOthersAsync_NullCurrentSessionId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.RevokeOthersAsync("user-1", null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 3: SendPasswordResetEmailAsync tests ---

    [Fact]
    public async Task SendPasswordResetEmailAsync_SendsPutWithUpdatePasswordAction()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SendPasswordResetEmailAsync("user-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PUT");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-1/execute-actions-email");
        _handler.Requests[0].Body.ShouldContain("UPDATE_PASSWORD");
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.SendPasswordResetEmailAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 3: SetTemporaryPasswordAsync tests ---

    [Fact]
    public async Task SetTemporaryPasswordAsync_SendsPutWithTemporaryPassword()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetTemporaryPasswordAsync("user-1", "TempPass123!", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PUT");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-1/reset-password");
        _handler.Requests[0].Body.ShouldContain("\"type\":\"password\"");
        _handler.Requests[0].Body.ShouldContain("\"value\":\"TempPass123!\"");
        _handler.Requests[0].Body.ShouldContain("\"temporary\":true");
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.SetTemporaryPasswordAsync(null!, "pass", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_NullPassword_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.SetTemporaryPasswordAsync("user-1", null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 4: CreateUserAsync tests ---

    [Fact]
    public async Task CreateUserAsync_SendsPostAndReturnsCreatedUser()
    {
        MockHttpMessageHandlerWithLocation locationHandler = new()
        {
            LocationPath = "/admin/realms/test-realm/users/new-user-id",
        };

        HttpClient locationClient = new(locationHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory locationFactory = Substitute.For<IHttpClientFactory>();
        locationFactory.CreateClient("KeycloakAdmin").Returns(locationClient);

        KeycloakIdentityProvider provider = new(
            _tokenService,
            _tokenExchangeService,
            locationFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            Substitute.For<IDistributedEventBus>(),
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);

        IdentityUserCreate newUser = new("alice", "alice@test.com", "Alice", "Doe");

        IIdentityUser result = await provider.CreateUserAsync(newUser, TestContext.Current.CancellationToken);

        result.UserId.ShouldBe("new-user-id");
        result.Username.ShouldBe("alice");
        result.Email.ShouldBe("alice@test.com");
        result.FirstName.ShouldBe("Alice");
        result.LastName.ShouldBe("Doe");
        result.Enabled.ShouldBeTrue();

        locationHandler.Requests.Count.ShouldBe(1);
        locationHandler.Requests[0].Method.ShouldBe("POST");
        locationHandler.Requests[0].Body.ShouldContain("\"username\":\"alice\"");
    }

    [Fact]
    public async Task CreateUserAsync_NullUser_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.CreateUserAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 5: GetGroupsAsync tests ---

    [Fact]
    public async Task GetGroupsAsync_WithGroups_ReturnsIdentityGroups()
    {
        _handler.ResponseBody = """[{"id":"grp-1","name":"Developers","path":"/Developers","subGroups":[]},{"id":"grp-2","name":"Admins","path":"/Admins","subGroups":[{"id":"grp-3","name":"Super Admins","path":"/Admins/Super Admins","subGroups":[]}]}]""";

        IReadOnlyList<IdentityGroup> result = await _provider.GetGroupsAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("grp-1");
        result[0].Name.ShouldBe("Developers");
        result[0].Path.ShouldBe("/Developers");
        result[0].SubGroups.ShouldBeEmpty();
        result[1].SubGroups.Count.ShouldBe(1);
        result[1].SubGroups[0].Name.ShouldBe("Super Admins");
    }

    [Fact]
    public async Task GetGroupsAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetGroupsAsync(TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/groups");
    }

    [Fact]
    public async Task GetGroupsAsync_KeycloakError_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IdentityGroup> result = await _provider.GetGroupsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // --- Feature 5: GetUserGroupsAsync tests ---

    [Fact]
    public async Task GetUserGroupsAsync_WithGroups_ReturnsIdentityGroups()
    {
        _handler.ResponseBody = """[{"id":"grp-1","name":"Developers","path":"/Developers","subGroups":[]}]""";

        IReadOnlyList<IdentityGroup> result = await _provider.GetUserGroupsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Developers");
    }

    [Fact]
    public async Task GetUserGroupsAsync_CallsCorrectEndpoint()
    {
        _handler.ResponseBody = "[]";

        await _provider.GetUserGroupsAsync("user-abc", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-abc/groups");
    }

    [Fact]
    public async Task GetUserGroupsAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.GetUserGroupsAsync(null!, TestContext.Current.CancellationToken));
    }

    // --- Feature 5: AddUserToGroupAsync tests ---

    [Fact]
    public async Task AddUserToGroupAsync_SendsPutToCorrectEndpoint()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.AddUserToGroupAsync("user-1", "grp-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PUT");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-1/groups/grp-1");
    }

    [Fact]
    public async Task AddUserToGroupAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.AddUserToGroupAsync(null!, "grp-1", TestContext.Current.CancellationToken));
    }

    // --- Feature 5: RemoveUserFromGroupAsync tests ---

    [Fact]
    public async Task RemoveUserFromGroupAsync_SendsDeleteToCorrectEndpoint()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.RemoveUserFromGroupAsync("user-1", "grp-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("DELETE");
        _handler.Requests[0].Url.ShouldContain("/admin/realms/test-realm/users/user-1/groups/grp-1");
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_NullUserId_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.RemoveUserFromGroupAsync(null!, "grp-1", TestContext.Current.CancellationToken));
    }

    // --- UpdateUserAsync tests ---

    [Fact]
    public async Task UpdateUserAsync_PatchesAndPutsUser()
    {
        // First request = GET current user, second request = PUT updated user.
        MockSequenceHttpMessageHandler seqHandler = new(
        [
            """{"access_token":"fake-token","expires_in":300}""",
            """{"id":"user-1","username":"alice","email":"alice@test.com","firstName":"Alice","lastName":"Doe","enabled":true}""",
            "", // PUT response body (empty, 204-like)
        ]);
        HttpClient seqClient = new(seqHandler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory seqFactory = Substitute.For<IHttpClientFactory>();
        seqFactory.CreateClient("KeycloakAdmin").Returns(seqClient);

        IClock updateClock = Substitute.For<IClock>();
        updateClock.Now.Returns(DateTimeOffset.UtcNow);

        KeycloakAdminTokenService tokenSvc = new(
            seqFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            updateClock,
            NullLogger<KeycloakAdminTokenService>.Instance);

        KeycloakIdentityProvider provider = new(
            tokenSvc,
            _tokenExchangeService,
            seqFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            Substitute.For<IDistributedEventBus>(),
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);

        IdentityUserUpdate update = new(Email: "newalice@test.com", FirstName: "Alicia");

        await provider.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        // 3 calls: token request, GET user, PUT updated user
        seqHandler.CallCount.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateUserAsync_NullUserId_ThrowsArgumentNullException()
    {
        IdentityUserUpdate update = new("a@b.com");

        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.UpdateUserAsync(null!, update, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateUserAsync_NullUpdate_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.UpdateUserAsync("user-1", null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateUserAsync_KeycloakError_PropagatesException()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Forbidden;
        _handler.ResponseBody = string.Empty;

        IdentityUserUpdate update = new("a@b.com");

        await Should.ThrowAsync<HttpRequestException>(
            () => _provider.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken));
    }

    // --- VerifyUserCredentialsAsync tests ---

    [Fact]
    public async Task VerifyUserCredentialsAsync_ValidCredentials_ReturnsTrue()
    {
        KeycloakAdminOptions optionsWithDirect = new()
        {
            BaseUrl = "https://keycloak.test",
            Realm = "test-realm",
            ClientId = "admin-service",
            ClientSecret = "secret",
            DirectAccessClientId = "test-frontend",
        };

        MockHttpMessageHandler handler = new()
        {
            ResponseBody = """{"access_token":"user-token","expires_in":300}""",
        };

        HttpClient client = new(handler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("KeycloakAdmin").Returns(client);

        KeycloakIdentityProvider provider = new(
            _tokenService,
            _tokenExchangeService,
            factory,
            Microsoft.Extensions.Options.Options.Create(optionsWithDirect),
            Substitute.For<IDistributedEventBus>(),
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);

        bool result = await provider.VerifyUserCredentialsAsync("admin", "password123",
            TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Url.ShouldContain("/realms/test-realm/protocol/openid-connect/token");
        handler.Requests[0].Body.ShouldContain("grant_type=password");
        handler.Requests[0].Body.ShouldContain("client_id=test-frontend");
        handler.Requests[0].Body.ShouldContain("username=admin");
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_InvalidCredentials_ReturnsFalse()
    {
        KeycloakAdminOptions optionsWithDirect = new()
        {
            BaseUrl = "https://keycloak.test",
            Realm = "test-realm",
            ClientId = "admin-service",
            ClientSecret = "secret",
            DirectAccessClientId = "test-frontend",
        };

        MockHttpMessageHandler handler = new()
        {
            ResponseStatusCode = HttpStatusCode.Unauthorized,
            ResponseBody = """{"error":"invalid_grant"}""",
        };

        HttpClient client = new(handler) { BaseAddress = new Uri("https://keycloak.test/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("KeycloakAdmin").Returns(client);

        KeycloakIdentityProvider provider = new(
            _tokenService,
            _tokenExchangeService,
            factory,
            Microsoft.Extensions.Options.Options.Create(optionsWithDirect),
            Substitute.For<IDistributedEventBus>(),
            _metrics,
            NullLogger<KeycloakIdentityProvider>.Instance);

        bool result = await provider.VerifyUserCredentialsAsync("admin", "wrong-password",
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_NoDirectAccessClientId_ThrowsInvalidOperation()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _provider.VerifyUserCredentialsAsync("admin", "pass",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_NullUsername_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.VerifyUserCredentialsAsync(null!, "pass",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_NullPassword_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.VerifyUserCredentialsAsync("admin", null!,
                TestContext.Current.CancellationToken));
    }

    // --- Domain event publishing tests ---

    [Fact]
    public async Task SetUserEnabledAsync_PublishesEnabledChangedEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetUserEnabledAsync("u1", true, TestContext.Current.CancellationToken);

        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityUserEnabledChangedEto>(e => e.UserId == "u1" && e.Enabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_PublishesRoleAssignedEvent()
    {
        _handler.ResponseBody = """{"id":"role-1","name":"editor","description":"Content editor"}""";

        await _provider.AssignRoleAsync("u1", "editor", TestContext.Current.CancellationToken);

        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityRoleAssignedEto>(e => e.UserId == "u1" && e.RoleName == "editor"),
            Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _httpClient.Dispose();
        _metricsServiceProvider.Dispose();
    }
}

/// <summary>
/// Mock handler that returns a Location header on 201 Created responses (for user creation tests).
/// </summary>
internal sealed class MockHttpMessageHandlerWithLocation : HttpMessageHandler
{
    public List<(string Method, string Url, string Body)> Requests { get; } = [];
    public string LocationPath { get; set; } = string.Empty;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is not null
            ? await request.Content.ReadAsStringAsync(cancellationToken)
            : string.Empty;

        Requests.Add((request.Method.Method, request.RequestUri?.ToString() ?? "", body));

        HttpResponseMessage response = new(HttpStatusCode.Created)
        {
            Content = new StringContent(string.Empty),
        };
        response.Headers.Location = new Uri($"https://keycloak.test{LocationPath}");
        return response;
    }
}
