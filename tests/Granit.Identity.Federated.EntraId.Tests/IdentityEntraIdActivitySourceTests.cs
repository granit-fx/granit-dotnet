using System.Diagnostics;
using System.Net;
using Granit.Core.Events;
using Granit.Identity;
using Granit.Identity.Federated.EntraId.Diagnostics;
using Granit.Identity.Federated.EntraId.Internal;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class IdentityEntraIdActivitySourceTests : IDisposable
{
    private readonly List<Activity> _activities = [];
    private readonly ActivityListener _listener;
    private readonly MockHttpMessageHandler _handler = new();
    private readonly HttpClient _httpClient;
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly EntraIdAdminTokenService _tokenService;
    private readonly IPasswordResetNotifier _passwordResetNotifier = Substitute.For<IPasswordResetNotifier>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly EntraIdIdentityProvider _provider;

    public IdentityEntraIdActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == IdentityEntraIdActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _activities.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);

        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        _httpClientFactory.CreateClient("MicrosoftGraph").Returns(_httpClient);

#pragma warning disable GRSEC003 // Test setup, not real secrets
        var options = new EntraIdAdminOptions
        {
            TenantId = "test-tenant",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            ServicePrincipalObjectId = "sp-id",
        };
#pragma warning restore GRSEC003

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
            MsOptions.Create(options),
            clock,
            NullLogger<EntraIdAdminTokenService>.Instance);

        _provider = new EntraIdIdentityProvider(
            _tokenService,
            _httpClientFactory,
            MsOptions.Create(options),
            _passwordResetNotifier,
            _distributedEventBus,
            NullLogger<EntraIdIdentityProvider>.Instance);
    }

    [Fact]
    public async Task GetUsersAsync_EmitsSpan()
    {
        _handler.ResponseBody = """{"value":[]}""";

        await _provider.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken);

        _activities.ShouldContain(a => a.OperationName == IdentityEntraIdActivitySource.GetUsers);
    }

    [Fact]
    public async Task GetUserAsync_EmitsSpanWithUserIdTag()
    {
        _handler.ResponseBody = """{"id":"u1","userPrincipalName":"a@b.com","mail":null,"givenName":null,"surname":null,"accountEnabled":true}""";

        await _provider.GetUserAsync("u1", TestContext.Current.CancellationToken);

        Activity? activity = _activities.Find(a =>
            a.OperationName == IdentityEntraIdActivitySource.GetUser
            && a.GetTagItem("identity.user_id") is "u1");
        activity.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetUsersAsync_WhenFails_SetsErrorStatus()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        await _provider.GetUsersAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Filter by both operation name AND status to avoid cross-contamination from parallel test classes
        // (EntraIdIdentityProviderTests also calls GetUsersAsync with success, producing Unset activities).
        _activities.ShouldContain(a =>
            a.OperationName == IdentityEntraIdActivitySource.GetUsers
            && a.Status == ActivityStatusCode.Error);
    }

    [Fact]
    public async Task SetUserEnabledAsync_EmitsSpan()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetUserEnabledAsync("u1", true, TestContext.Current.CancellationToken);

        Activity? activity = _activities.Find(a =>
            a.OperationName == IdentityEntraIdActivitySource.SetUserEnabled
            && a.GetTagItem("identity.user_id") is "u1");
        activity.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetGroupsAsync_EmitsSpan()
    {
        _handler.ResponseBody = """{"value":[]}""";

        await _provider.GetGroupsAsync(TestContext.Current.CancellationToken);

        _activities.ShouldContain(a => a.OperationName == IdentityEntraIdActivitySource.GetGroups);
    }

    [Fact]
    public async Task GetGroupsAsync_WhenFails_SetsErrorStatus()
    {
        _handler.ResponseStatusCode = HttpStatusCode.InternalServerError;
        _handler.ResponseBody = string.Empty;

        await _provider.GetGroupsAsync(TestContext.Current.CancellationToken);

        // Filter by both operation name AND status to avoid cross-contamination from parallel test classes
        // (EntraIdIdentityProviderTests also calls GetGroupsAsync with success, producing Unset activities).
        _activities.ShouldContain(a =>
            a.OperationName == IdentityEntraIdActivitySource.GetGroups
            && a.Status == ActivityStatusCode.Error);
    }

    [Fact]
    public async Task TerminateAllSessionsAsync_EmitsSpan()
    {
        _handler.ResponseStatusCode = HttpStatusCode.OK;
        _handler.ResponseBody = """{"value":true}""";

        await _provider.TerminateAllSessionsAsync("u1", TestContext.Current.CancellationToken);

        // Filter by both operation name AND tag to avoid cross-contamination from parallel test classes.
        Activity? activity = _activities.Find(a =>
            a.OperationName == IdentityEntraIdActivitySource.TerminateAllSessions
            && a.GetTagItem("identity.user_id") is "u1");
        activity.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetRolesAsync_WhenFails_SetsErrorStatus()
    {
        _handler.ResponseStatusCode = HttpStatusCode.Forbidden;
        _handler.ResponseBody = string.Empty;

        await _provider.GetRolesAsync(TestContext.Current.CancellationToken);

        // Filter by both operation name AND status to avoid cross-contamination from parallel test classes
        // (EntraIdIdentityProviderAdditionalTests also calls GetRolesAsync with success, producing Unset activities).
        _activities.ShouldContain(a =>
            a.OperationName == IdentityEntraIdActivitySource.GetRoles
            && a.Status == ActivityStatusCode.Error);
    }

    [Fact]
    public async Task TokenAcquire_EmitsSpan()
    {
        // The token service acquires a token on first call.
        // The provider's GetUsersAsync triggers CreateAuthenticatedClientAsync → GetTokenAsync.
        // Token was already acquired in constructor setup — create a fresh token service to capture the span.
        MockHttpMessageHandler freshTokenHandler = new()
        {
            ResponseBody = """{"access_token":"new-token","expires_in":300}""",
        };
        HttpClient freshTokenClient = new(freshTokenHandler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory freshTokenFactory = Substitute.For<IHttpClientFactory>();
        freshTokenFactory.CreateClient("MicrosoftGraph").Returns(freshTokenClient);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        EntraIdAdminTokenService freshTokenService = new(
            freshTokenFactory,
#pragma warning disable GRSEC003
            MsOptions.Create(new EntraIdAdminOptions
            {
                TenantId = "t",
                ClientId = "c",
                ClientSecret = "s",
                ServicePrincipalObjectId = "sp",
            }),
#pragma warning restore GRSEC003
            clock,
            NullLogger<EntraIdAdminTokenService>.Instance);

        await freshTokenService.GetTokenAsync(TestContext.Current.CancellationToken);

        _activities.ShouldContain(a => a.OperationName == IdentityEntraIdActivitySource.TokenAcquire);
    }

    [Fact]
    public void ActivitySource_HasCorrectName() =>
        IdentityEntraIdActivitySource.Name.ShouldBe("Granit.Identity.EntraId");

    [Fact]
    public async Task SpansDoNotContainPii()
    {
        _handler.ResponseBody = """{"id":"u1","userPrincipalName":"alice@contoso.com","mail":"alice@test.com","givenName":"Alice","surname":"Doe","accountEnabled":true}""";

        await _provider.GetUserAsync("u1", TestContext.Current.CancellationToken);

        Activity? activity = _activities.Find(a => a.OperationName == IdentityEntraIdActivitySource.GetUser);
        activity.ShouldNotBeNull();

        // Verify no PII tags (email, name, etc.)
        foreach (KeyValuePair<string, string?> tag in activity.Tags)
        {
            tag.Key.ShouldNotContain("email");
            tag.Key.ShouldNotContain("name");
            string? value = tag.Value;
            if (value is not null)
            {
                value.ShouldNotContain("alice");
            }
        }
    }

    public void Dispose()
    {
        _listener.Dispose();
        _httpClient.Dispose();
        _tokenService.Dispose();
    }
}
