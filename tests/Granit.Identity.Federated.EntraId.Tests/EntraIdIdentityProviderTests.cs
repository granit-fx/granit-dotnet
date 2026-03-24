using System.Diagnostics;
using System.Net;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Events;
using Granit.Identity.Federated.EntraId.Internal;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdIdentityProviderTests : IDisposable
{
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly EntraIdAdminOptions _options = new()
    {
        TenantId = "test-tenant-id",
        ClientId = "admin-service",
        ClientSecret = "secret",
        ServicePrincipalObjectId = "sp-object-id",
    };

    private readonly EntraIdAdminTokenService _tokenService;
    private readonly IPasswordResetNotifier _passwordResetNotifier = Substitute.For<IPasswordResetNotifier>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly EntraIdIdentityProvider _provider;
    private readonly ActivityListener _activityListener;

    public EntraIdIdentityProviderTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Identity.EntraId",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        _httpClientFactory.CreateClient("MicrosoftGraph").Returns(_httpClient);

        // Token service returns a fake admin token.
        MockHttpMessageHandler tokenHandler = new()
        {
            ResponseBody = """{"access_token":"fake-token","expires_in":300}""",
        };
        HttpClient tokenClient = new(tokenHandler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory tokenFactory = Substitute.For<IHttpClientFactory>();
        tokenFactory.CreateClient("MicrosoftGraph").Returns(tokenClient);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        _tokenService = new EntraIdAdminTokenService(
            tokenFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            clock,
            NullLogger<EntraIdAdminTokenService>.Instance);

        _provider = new EntraIdIdentityProvider(
            _tokenService,
            _httpClientFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _passwordResetNotifier,
            _distributedEventBus,
            NullLogger<EntraIdIdentityProvider>.Instance);
    }

    // --- GetUsersAsync tests ---

    [Fact]
    public async Task GetUsersAsync_ReturnsUsers()
    {
        _handler.ResponseBody = """{"value":[{"id":"user-1","userPrincipalName":"alice@contoso.com","mail":"alice@test.com","givenName":"Alice","surname":"Doe","accountEnabled":true},{"id":"user-2","userPrincipalName":"bob@contoso.com","mail":null,"givenName":null,"surname":null,"accountEnabled":true}]}""";

        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].UserId.ShouldBe("user-1");
        result[0].Username.ShouldBe("alice@contoso.com");
        result[0].Email.ShouldBe("alice@test.com");
        result[0].FirstName.ShouldBe("Alice");
        result[0].LastName.ShouldBe("Doe");
        result[0].Enabled.ShouldBeTrue();
        result[1].UserId.ShouldBe("user-2");
        result[1].Username.ShouldBe("bob@contoso.com");
    }

    [Fact]
    public async Task GetUsersAsync_WhenHttpFails_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // --- GetUserAsync tests ---

    [Fact]
    public async Task GetUserAsync_ReturnsUser()
    {
        _handler.ResponseBody = """{"id":"user-1","userPrincipalName":"alice@contoso.com","mail":"alice@test.com","givenName":"Alice","surname":"Doe","accountEnabled":true}""";

        IIdentityUser? result = await _provider.GetUserAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe("user-1");
        result.Username.ShouldBe("alice@contoso.com");
        result.Email.ShouldBe("alice@test.com");
        result.FirstName.ShouldBe("Alice");
        result.LastName.ShouldBe("Doe");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserAsync_WhenHttpFails_ReturnsNull()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NotFound;
        _handler.ResponseBody = string.Empty;

        IIdentityUser? result = await _provider.GetUserAsync(
            "unknown", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // --- SetUserEnabledAsync tests ---

    [Fact]
    public async Task SetUserEnabledAsync_SendsPatchRequest()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetUserEnabledAsync("user-1", true, TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PATCH");
        _handler.Requests[0].Url.ShouldContain("/v1.0/users/user-1");
        _handler.Requests[0].Body.ShouldContain("\"accountEnabled\":true");
    }

    [Fact]
    public async Task SetUserEnabledAsync_Disable_SendsAccountEnabledFalse()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetUserEnabledAsync("user-1", false, TestContext.Current.CancellationToken);

        _handler.Requests[0].Body.ShouldContain("\"accountEnabled\":false");
    }

    // --- GetGroupsAsync tests ---

    [Fact]
    public async Task GetGroupsAsync_ReturnsGroups_WithEmptySubGroups()
    {
        _handler.ResponseBody = """{"value":[{"id":"grp-1","displayName":"Developers","description":"Dev team"},{"id":"grp-2","displayName":"Admins","description":null}]}""";

        IReadOnlyList<IdentityGroup> result = await _provider.GetGroupsAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("grp-1");
        result[0].Name.ShouldBe("Developers");
        result[0].Path.ShouldBeNull();
        result[0].SubGroups.ShouldBeEmpty();
        result[1].Id.ShouldBe("grp-2");
        result[1].Name.ShouldBe("Admins");
        result[1].SubGroups.ShouldBeEmpty();
    }

    // --- TerminateSessionAsync tests ---

    [Fact]
    public async Task TerminateSessionAsync_RevokesAllSessions()
    {
        _handler.ResponseStatusCode = HttpStatusCode.OK;
        _handler.ResponseBody = """{"value":true}""";

        await _provider.TerminateSessionAsync("user-1", "sess-abc", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("POST");
        _handler.Requests[0].Url.ShouldContain("/v1.0/users/user-1/revokeSignInSessions");
    }

    // --- VerifyUserCredentialsAsync tests ---

    [Fact]
    public async Task VerifyUserCredentialsAsync_ReturnsTrue_OnSuccess()
    {
        var optionsWithRopc = new EntraIdAdminOptions
        {
            TenantId = "test-tenant-id",
            ClientId = "admin-service",
            ClientSecret = "secret",
            ServicePrincipalObjectId = "sp-object-id",
            RopcClientId = "ropc-client",
        };

        MockHttpMessageHandler handler = new()
        {
            ResponseBody = """{"access_token":"user-token","expires_in":300}""",
        };

        HttpClient client = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(client);

        EntraIdIdentityProvider provider = new(
            _tokenService,
            factory,
            Microsoft.Extensions.Options.Options.Create(optionsWithRopc),
            _passwordResetNotifier,
            _distributedEventBus,
            NullLogger<EntraIdIdentityProvider>.Instance);

        bool result = await provider.VerifyUserCredentialsAsync("admin", "password123",
            TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Url.ShouldContain("login.microsoftonline.com/test-tenant-id/oauth2/v2.0/token");
        handler.Requests[0].Body.ShouldContain("grant_type=password");
        handler.Requests[0].Body.ShouldContain("client_id=ropc-client");
        handler.Requests[0].Body.ShouldContain("username=admin");
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_ReturnsFalse_OnFailure()
    {
        var optionsWithRopc = new EntraIdAdminOptions
        {
            TenantId = "test-tenant-id",
            ClientId = "admin-service",
            ClientSecret = "secret",
            ServicePrincipalObjectId = "sp-object-id",
            RopcClientId = "ropc-client",
        };

        MockHttpMessageHandler handler = new()
        {
            ResponseStatusCode = HttpStatusCode.Unauthorized,
            ResponseBody = """{"error":"invalid_grant"}""",
        };

        HttpClient client = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(client);

        EntraIdIdentityProvider provider = new(
            _tokenService,
            factory,
            Microsoft.Extensions.Options.Options.Create(optionsWithRopc),
            _passwordResetNotifier,
            _distributedEventBus,
            NullLogger<EntraIdIdentityProvider>.Instance);

        bool result = await provider.VerifyUserCredentialsAsync("admin", "wrong-password",
            TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_ThrowsWhenRopcClientIdNotConfigured()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _provider.VerifyUserCredentialsAsync("admin", "pass",
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
    public async Task CreateUserAsync_PublishesUserCreatedEvent()
    {
        var optionsWithDomain = new EntraIdAdminOptions
        {
            TenantId = "test-tenant-id",
            ClientId = "admin-service",
            ClientSecret = "secret",
            ServicePrincipalObjectId = "sp-object-id",
            DefaultDomain = "contoso.com",
        };

        MockHttpMessageHandler handler = new()
        {
            ResponseBody = """{"id":"new-user-id","userPrincipalName":"alice@contoso.com","mail":"alice@test.com","givenName":"Alice","surname":"Doe","accountEnabled":true}""",
        };

        HttpClient client = new(handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(client);

        IDistributedEventBus distributedEventBus = Substitute.For<IDistributedEventBus>();

        EntraIdIdentityProvider provider = new(
            _tokenService,
            factory,
            Microsoft.Extensions.Options.Options.Create(optionsWithDomain),
            _passwordResetNotifier,
            distributedEventBus,
            NullLogger<EntraIdIdentityProvider>.Instance);

        IdentityUserCreate newUser = new("alice", "alice@test.com", "Alice", "Doe");

        await provider.CreateUserAsync(newUser, TestContext.Current.CancellationToken);

        await distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityUserCreatedEto>(e =>
                e.UserId == "new-user-id" &&
                e.Username == "alice" &&
                e.Email == "alice@test.com"),
            Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _httpClient.Dispose();
    }
}
