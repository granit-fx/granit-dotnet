using System.Diagnostics;
using System.Net;
using Granit.Identity;
using Granit.Identity.EntraId.Internal;
using Granit.Identity.EntraId.Options;
using Granit.Identity.Events;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntraId.Tests;

public sealed class EntraIdIdentityProviderAdditionalTests : IDisposable
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
    private readonly IIdentityEventPublisher _eventPublisher = Substitute.For<IIdentityEventPublisher>();
    private readonly EntraIdIdentityProvider _provider;
    private readonly ActivityListener _activityListener;

    public EntraIdIdentityProviderAdditionalTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Identity.EntraId",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);

        _httpClient = new HttpClient(_handler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        _httpClientFactory.CreateClient("MicrosoftGraph").Returns(_httpClient);

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
            _eventPublisher,
            NullLogger<EntraIdIdentityProvider>.Instance);
    }

    // ──── UpdateUserAsync ────

    [Fact]
    public async Task UpdateUserAsync_SendsPatchWithAllFields()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        var update = new IdentityUserUpdate(
            Email: "new@contoso.com",
            FirstName: "Jane",
            LastName: "Smith");

        await _provider.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PATCH");
        _handler.Requests[0].Url.ShouldContain("/v1.0/users/user-1");
        _handler.Requests[0].Body.ShouldContain("\"mail\":\"new@contoso.com\"");
        _handler.Requests[0].Body.ShouldContain("\"givenName\":\"Jane\"");
        _handler.Requests[0].Body.ShouldContain("\"surname\":\"Smith\"");
    }

    [Fact]
    public async Task UpdateUserAsync_WithCustomAttributes_IncludesExtensionAttributes()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        var update = new IdentityUserUpdate(
            Attributes: new Dictionary<string, string?>
            {
                ["extensionAttribute1"] = "value1",
                ["extensionAttribute2"] = "value2",
            });

        await _provider.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Body.ShouldContain("onPremisesExtensionAttributes");
        _handler.Requests[0].Body.ShouldContain("extensionAttribute1");
        _handler.Requests[0].Body.ShouldContain("value1");
    }

    [Fact]
    public async Task UpdateUserAsync_PublishesProfileUpdatedEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        var update = new IdentityUserUpdate(Email: "new@contoso.com");

        await _provider.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<IdentityUserProfileUpdatedEvent>(e =>
                e.UserId == "user-1" && e.Update == update),
            Arg.Any<CancellationToken>());
    }

    // ──── GetUserSessionsAsync ────

    [Fact]
    public async Task GetUserSessionsAsync_ReturnsOnlySuccessfulSignIns()
    {
        _handler.ResponseBody = """
            {
                "value": [
                    {
                        "id": "s1",
                        "ipAddress": "10.0.0.1",
                        "createdDateTime": "2026-01-01T10:00:00Z",
                        "clientAppUsed": "Browser",
                        "deviceDetail": { "browser": "Chrome", "operatingSystem": "Windows" },
                        "status": { "errorCode": 0 }
                    },
                    {
                        "id": "s2",
                        "ipAddress": "10.0.0.2",
                        "createdDateTime": "2026-01-02T10:00:00Z",
                        "clientAppUsed": null,
                        "deviceDetail": null,
                        "status": { "errorCode": 50126 }
                    }
                ]
            }
            """;

        IReadOnlyList<IdentitySession> result = await _provider.GetUserSessionsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SessionId.ShouldBe("s1");
        result[0].IpAddress.ShouldBe("10.0.0.1");
        result[0].Clients.Count.ShouldBe(1);
        result[0].Clients[0].ShouldBe("Browser");
    }

    [Fact]
    public async Task GetUserSessionsAsync_WhenHttpFails_ReturnsEmptyList()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        IReadOnlyList<IdentitySession> result = await _provider.GetUserSessionsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ──── GetUserDeviceActivityAsync ────

    [Fact]
    public async Task GetUserDeviceActivityAsync_GroupsByIpAndOs()
    {
        _handler.ResponseBody = """
            {
                "value": [
                    {
                        "id": "s1",
                        "ipAddress": "10.0.0.1",
                        "createdDateTime": "2026-01-02T10:00:00Z",
                        "clientAppUsed": "Browser",
                        "deviceDetail": { "browser": "Chrome/120.0", "operatingSystem": "Windows" },
                        "status": { "errorCode": 0 }
                    },
                    {
                        "id": "s2",
                        "ipAddress": "10.0.0.1",
                        "createdDateTime": "2026-01-01T10:00:00Z",
                        "clientAppUsed": "Browser",
                        "deviceDetail": { "browser": "Chrome/119.0", "operatingSystem": "Windows" },
                        "status": { "errorCode": 0 }
                    },
                    {
                        "id": "s3",
                        "ipAddress": "10.0.0.2",
                        "createdDateTime": "2026-01-03T10:00:00Z",
                        "clientAppUsed": "Mobile App",
                        "deviceDetail": { "browser": "Safari", "operatingSystem": "Android" },
                        "status": { "errorCode": 0 }
                    }
                ]
            }
            """;

        IReadOnlyList<IdentityDeviceActivity> result = await _provider.GetUserDeviceActivityAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);

        IdentityDeviceActivity desktop = result.First(d => d.Os == "Windows");
        desktop.IpAddress.ShouldBe("10.0.0.1");
        desktop.Device.ShouldBe("Desktop");
        desktop.Mobile.ShouldBeFalse();
        desktop.Sessions.Count.ShouldBe(2);
        desktop.Browser.ShouldBe("Chrome/120.0");

        IdentityDeviceActivity mobile = result.First(d => d.Os == "Android");
        mobile.IpAddress.ShouldBe("10.0.0.2");
        mobile.Device.ShouldBe("Mobile");
        mobile.Mobile.ShouldBeTrue();
        mobile.Sessions.Count.ShouldBe(1);
    }

    // ──── GetPasswordChangedAtAsync ────

    [Fact]
    public async Task GetPasswordChangedAtAsync_ReturnsDate()
    {
        _handler.ResponseBody = """{"lastPasswordChangeDateTime":"2026-03-01T12:00:00Z"}""";

        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Value.ShouldBe(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_WhenHttpFails_ReturnsNull()
    {
        _handler.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        _handler.ResponseBody = string.Empty;

        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── GetRolesAsync ────

    [Fact]
    public async Task GetRolesAsync_ReturnsOnlyEnabledRoles()
    {
        _handler.ResponseBody = """
            {
                "appRoles": [
                    { "id": "r1", "value": "Admin", "description": "Admin role", "isEnabled": true },
                    { "id": "r2", "value": "Disabled", "description": "Old", "isEnabled": false }
                ]
            }
            """;

        IReadOnlyList<IdentityRole> result = await _provider.GetRolesAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe("r1");
        result[0].Name.ShouldBe("Admin");
        result[0].Description.ShouldBe("Admin role");
    }

    // ──── GetUserRolesAsync ────

    [Fact]
    public async Task GetUserRolesAsync_CrossReferencesAssignmentsWithRoleDefinitions()
    {
        string assignmentsResponse = """
            {
                "value": [
                    { "id": "a1", "appRoleId": "role-id-1", "principalId": "user-1", "resourceId": "sp-object-id" },
                    { "id": "a2", "appRoleId": "role-id-2", "principalId": "user-1", "resourceId": "sp-object-id" }
                ]
            }
            """;

        string servicePrincipalResponse = """
            {
                "id": "sp-object-id",
                "appRoles": [
                    { "id": "role-id-1", "value": "Admin", "description": "Admin role", "isEnabled": true },
                    { "id": "role-id-2", "value": "Reader", "description": "Reader role", "isEnabled": true },
                    { "id": "role-id-3", "value": "Disabled", "description": "Old", "isEnabled": false }
                ]
            }
            """;

        MockSequenceHttpMessageHandler sequenceHandler = new([assignmentsResponse, servicePrincipalResponse]);
        HttpClient sequenceClient = new(sequenceHandler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory sequenceFactory = Substitute.For<IHttpClientFactory>();
        sequenceFactory.CreateClient("MicrosoftGraph").Returns(sequenceClient);

        EntraIdIdentityProvider provider = new(
            _tokenService,
            sequenceFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _passwordResetNotifier,
            _eventPublisher,
            NullLogger<EntraIdIdentityProvider>.Instance);

        IReadOnlyList<IdentityRole> result = await provider.GetUserRolesAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain(r => r.Name == "Admin");
        result.ShouldContain(r => r.Name == "Reader");
        sequenceHandler.CallCount.ShouldBe(2);
    }

    // ──── TerminateAllSessionsAsync ────

    [Fact]
    public async Task TerminateAllSessionsAsync_SendsPostToRevokeEndpoint()
    {
        _handler.ResponseStatusCode = HttpStatusCode.OK;
        _handler.ResponseBody = """{"value":true}""";

        await _provider.TerminateAllSessionsAsync("user-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("POST");
        _handler.Requests[0].Url.ShouldContain("/v1.0/users/user-1/revokeSignInSessions");
    }

    [Fact]
    public async Task TerminateAllSessionsAsync_PublishesSessionsRevokedEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.OK;
        _handler.ResponseBody = """{"value":true}""";

        await _provider.TerminateAllSessionsAsync("user-1", TestContext.Current.CancellationToken);

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<IdentitySessionsRevokedEvent>(e => e.UserId == "user-1"),
            Arg.Any<CancellationToken>());
    }

    // ──── SetTemporaryPasswordAsync ────

    [Fact]
    public async Task SetTemporaryPasswordAsync_SendsPatchWithPasswordProfile()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetTemporaryPasswordAsync(
            "user-1", "TempPass123!", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PATCH");
        _handler.Requests[0].Url.ShouldContain("/v1.0/users/user-1");
        _handler.Requests[0].Body.ShouldContain("passwordProfile");
        _handler.Requests[0].Body.ShouldContain("TempPass123!");
        _handler.Requests[0].Body.ShouldContain("forceChangePasswordNextSignIn");
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_PublishesPasswordResetEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SetTemporaryPasswordAsync(
            "user-1", "TempPass123!", TestContext.Current.CancellationToken);

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<IdentityPasswordResetEvent>(e => e.UserId == "user-1"),
            Arg.Any<CancellationToken>());
    }

    // ──── SendPasswordResetEmailAsync ────

    [Fact]
    public async Task SendPasswordResetEmailAsync_SetsTemporaryPasswordAndNotifies()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SendPasswordResetEmailAsync("user-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("PATCH");
        _handler.Requests[0].Body.ShouldContain("passwordProfile");

        await _passwordResetNotifier.Received(1).NotifyAsync(
            "user-1",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_PublishesPasswordResetEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.SendPasswordResetEmailAsync("user-1", TestContext.Current.CancellationToken);

        await _eventPublisher.Received().PublishAsync(
            Arg.Is<IdentityPasswordResetEvent>(e => e.UserId == "user-1"),
            Arg.Any<CancellationToken>());
    }

    // ──── GetUserGroupsAsync ────

    [Fact]
    public async Task GetUserGroupsAsync_ReturnsGroups()
    {
        _handler.ResponseBody = """
            {
                "value": [
                    { "id": "grp-1", "displayName": "Developers", "description": "Dev team" },
                    { "id": "grp-2", "displayName": "Admins", "description": null }
                ]
            }
            """;

        IReadOnlyList<IdentityGroup> result = await _provider.GetUserGroupsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Id.ShouldBe("grp-1");
        result[0].Name.ShouldBe("Developers");
        result[0].SubGroups.ShouldBeEmpty();
        result[1].Id.ShouldBe("grp-2");
        result[1].Name.ShouldBe("Admins");

        _handler.Requests[0].Url.ShouldContain("/v1.0/users/user-1/memberOf/microsoft.graph.group");
    }

    // ──── AddUserToGroupAsync ────

    [Fact]
    public async Task AddUserToGroupAsync_SendsPostWithOdataId()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.AddUserToGroupAsync("user-1", "grp-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("POST");
        _handler.Requests[0].Url.ShouldContain("/v1.0/groups/grp-1/members/$ref");
        _handler.Requests[0].Body.ShouldContain("directoryObjects/user-1");
    }

    [Fact]
    public async Task AddUserToGroupAsync_PublishesGroupMembershipChangedEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.AddUserToGroupAsync("user-1", "grp-1", TestContext.Current.CancellationToken);

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<IdentityGroupMembershipChangedEvent>(e =>
                e.UserId == "user-1" && e.GroupId == "grp-1" && e.Added),
            Arg.Any<CancellationToken>());
    }

    // ──── RemoveUserFromGroupAsync ────

    [Fact]
    public async Task RemoveUserFromGroupAsync_SendsDeleteRequest()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.RemoveUserFromGroupAsync("user-1", "grp-1", TestContext.Current.CancellationToken);

        _handler.Requests.Count.ShouldBe(1);
        _handler.Requests[0].Method.ShouldBe("DELETE");
        _handler.Requests[0].Url.ShouldContain("/v1.0/groups/grp-1/members/user-1/$ref");
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_PublishesGroupMembershipChangedEvent()
    {
        _handler.ResponseStatusCode = HttpStatusCode.NoContent;
        _handler.ResponseBody = string.Empty;

        await _provider.RemoveUserFromGroupAsync("user-1", "grp-1", TestContext.Current.CancellationToken);

        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<IdentityGroupMembershipChangedEvent>(e =>
                e.UserId == "user-1" && e.GroupId == "grp-1" && !e.Added),
            Arg.Any<CancellationToken>());
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _httpClient.Dispose();
    }
}
