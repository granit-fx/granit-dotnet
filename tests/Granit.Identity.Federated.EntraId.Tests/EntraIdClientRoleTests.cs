using System.Diagnostics;
using Granit.Events;
using Granit.Guids;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Internal;
using Granit.Identity.Federated.EntraId.Options;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

/// <summary>
/// Covers Phase 2 <see cref="IIdentityClientRoleManager"/> methods on
/// <see cref="EntraIdIdentityProvider"/> — HTTP mock-based, same pattern as
/// <c>EntraIdIdentityProviderTests</c>.
/// </summary>
[Collection("EntraIdActivitySource")]
public sealed class EntraIdClientRoleTests : IDisposable
{
    private readonly EntraIdAdminOptions _options = new()
    {
        TenantId = "test-tenant-id",
        ClientId = "admin-service",
        ClientSecret = "secret",
        ServicePrincipalObjectId = "sp-object-id",
    };

    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly IPasswordResetNotifier _passwordResetNotifier = Substitute.For<IPasswordResetNotifier>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly ActivityListener _activityListener;
    private HttpClient? _graphHttpClient;
    private HttpClient? _tokenHttpClient;
    private MockSequenceHttpMessageHandler? _graphHandler;

    public EntraIdClientRoleTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Granit.Identity.EntraId",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _graphHttpClient?.Dispose();
        _tokenHttpClient?.Dispose();
    }

    private EntraIdIdentityProvider BuildProvider(params string[] responses)
    {
        _graphHttpClient?.Dispose();
        _tokenHttpClient?.Dispose();

        _graphHandler = new MockSequenceHttpMessageHandler(responses);
        _graphHttpClient = new HttpClient(_graphHandler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        _httpClientFactory.CreateClient("MicrosoftGraph").Returns(_graphHttpClient);

        MockHttpMessageHandler tokenHandler = new()
        {
            ResponseBody = """{"access_token":"fake-token","expires_in":300}""",
        };
        _tokenHttpClient = new HttpClient(tokenHandler) { BaseAddress = new Uri("https://graph.microsoft.com/") };
        IHttpClientFactory tokenFactory = Substitute.For<IHttpClientFactory>();
        tokenFactory.CreateClient("MicrosoftGraph").Returns(_tokenHttpClient);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        EntraIdAdminTokenService tokenService = new(
            tokenFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            clock,
            NullLogger<EntraIdAdminTokenService>.Instance);

        return new EntraIdIdentityProvider(
            tokenService,
            _httpClientFactory,
            Microsoft.Extensions.Options.Options.Create(_options),
            _passwordResetNotifier,
            _distributedEventBus,
            new SimpleGuidGenerator(),
            NullLogger<EntraIdIdentityProvider>.Instance);
    }

    [Fact]
    public async Task GetClientsAsync_ReturnsAppIds_SkippingNullAppIds()
    {
        const string response = """
            {"value":[
              {"id":"sp-1","appId":"11111111-1111-1111-1111-111111111111","appRoles":[]},
              {"id":"sp-2","appId":"22222222-2222-2222-2222-222222222222","appRoles":[]},
              {"id":"sp-3","appId":null,"appRoles":[]}
            ]}
            """;
        EntraIdIdentityProvider provider = BuildProvider(response);

        IReadOnlyList<string> clients = await provider.GetClientsAsync(TestContext.Current.CancellationToken);

        clients.ShouldBe([
            "11111111-1111-1111-1111-111111111111",
            "22222222-2222-2222-2222-222222222222",
        ]);
    }

    [Fact]
    public async Task GetClientRolesAsync_ResolvesServicePrincipal_ProjectsEnabledAppRoles()
    {
        const string appId = "11111111-1111-1111-1111-111111111111";
        string resolveResponse = $$"""
            {"value":[{
              "id":"sp-object-id-1",
              "appId":"{{appId}}",
              "appRoles":[
                {"id":"r1","displayName":"Editor","value":"Editor","description":"Edit docs","isEnabled":true},
                {"id":"r2","displayName":"Viewer","value":"Viewer","description":null,"isEnabled":true},
                {"id":"r3","displayName":"Disabled","value":"Disabled","description":null,"isEnabled":false}
              ]
            }]}
            """;
        EntraIdIdentityProvider provider = BuildProvider(resolveResponse);

        IReadOnlyList<IdentityRole> roles = await provider.GetClientRolesAsync(
            appId, TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(2);
        roles[0].Name.ShouldBe("Editor");
        roles[0].ClientId.ShouldBe(appId);
        roles[0].Description.ShouldBe("Edit docs");
        roles[1].Name.ShouldBe("Viewer");
        roles[1].ClientId.ShouldBe(appId);
    }

    [Fact]
    public async Task GetClientRolesAsync_UnknownAppId_Throws()
    {
        const string emptyResolve = """{"value":[]}""";
        EntraIdIdentityProvider provider = BuildProvider(emptyResolve);

        EntraIdClientNotFoundException ex = await Should.ThrowAsync<EntraIdClientNotFoundException>(
            async () => await provider.GetClientRolesAsync(
                "nonexistent-app-id", TestContext.Current.CancellationToken));

        ex.AppId.ShouldBe("nonexistent-app-id");
    }

    [Fact]
    public async Task GetUserClientRolesAsync_ResolvesThenMapsAssignments()
    {
        const string appId = "22222222-2222-2222-2222-222222222222";
        string resolveResponse = $$"""
            {"value":[{
              "id":"sp-object-id-2",
              "appId":"{{appId}}",
              "appRoles":[
                {"id":"r-admin","displayName":"Admin","value":"Admin","description":null,"isEnabled":true},
                {"id":"r-view","displayName":"Viewer","value":"Viewer","description":null,"isEnabled":true}
              ]
            }]}
            """;
        const string assignmentsResponse = """
            {"value":[
              {"id":"a1","appRoleId":"r-admin","principalId":"user-42","resourceId":"sp-object-id-2"}
            ]}
            """;
        EntraIdIdentityProvider provider = BuildProvider(resolveResponse, assignmentsResponse);

        IReadOnlyList<IdentityRole> roles = await provider.GetUserClientRolesAsync(
            "user-42", appId, TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(1);
        roles[0].Name.ShouldBe("Admin");
        roles[0].ClientId.ShouldBe(appId);
    }

    // ──── ADR-031 — client-role writes ────────────────────────────────────

    [Fact]
    public async Task CreateClientRoleAsync_GetsApplication_ThenPatchesAppRolesArray()
    {
        const string appId = "11111111-1111-1111-1111-111111111111";
        const string appObjectId = "app-object-id-1";
        // 1st response = GET /applications (resolve app object id + existing appRoles)
        string applicationResponse = $$"""
            {"value":[{
              "id":"{{appObjectId}}",
              "appId":"{{appId}}",
              "appRoles":[
                {"id":"r1","displayName":"Existing","value":"Existing","description":null,"isEnabled":true,"allowedMemberTypes":["User"]}
              ]
            }]}
            """;
        // 2nd response = PATCH /applications/{objectId} — body ignored
        const string patchResponse = "{}";
        EntraIdIdentityProvider provider = BuildProvider(applicationResponse, patchResponse);

        IdentityRole created = await provider.CreateClientRoleAsync(
            appId, "Editor", "Edit docs", TestContext.Current.CancellationToken);

        created.Id.ShouldNotBeNullOrWhiteSpace();
        created.Name.ShouldBe("Editor");
        created.Description.ShouldBe("Edit docs");
        created.ClientId.ShouldBe(appId);

        _graphHandler!.Requests.Count.ShouldBe(2);
        _graphHandler.Requests[0].Method.ShouldBe("GET");
        _graphHandler.Requests[0].Url.ShouldContain("/v1.0/applications");
        _graphHandler.Requests[1].Method.ShouldBe("PATCH");
        _graphHandler.Requests[1].Url.ShouldContain($"/v1.0/applications/{appObjectId}");
        // Body MUST contain both the pre-existing role AND the new one (concurrency-unsafe PATCH).
        _graphHandler.Requests[1].Body.ShouldContain("Existing");
        _graphHandler.Requests[1].Body.ShouldContain("Editor");
    }

    [Fact]
    public async Task CreateClientRoleAsync_UnknownAppId_Throws()
    {
        const string emptyResolve = """{"value":[]}""";
        EntraIdIdentityProvider provider = BuildProvider(emptyResolve);

        await Should.ThrowAsync<EntraIdClientNotFoundException>(
            async () => await provider.CreateClientRoleAsync(
                "nonexistent-app-id", "Editor", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AssignClientRoleAsync_ResolvesSp_ThenPostsAppRoleAssignment()
    {
        const string appId = "11111111-1111-1111-1111-111111111111";
        string resolveResponse = $$"""
            {"value":[{
              "id":"sp-object-id-1",
              "appId":"{{appId}}",
              "appRoles":[
                {"id":"r-editor","displayName":"Editor","value":"Editor","description":null,"isEnabled":true}
              ]
            }]}
            """;
        const string postResponse = "{}";
        EntraIdIdentityProvider provider = BuildProvider(resolveResponse, postResponse);

        await provider.AssignClientRoleAsync(
            "user-42", appId, "Editor", TestContext.Current.CancellationToken);

        _graphHandler!.Requests.Count.ShouldBe(2);
        _graphHandler.Requests[0].Method.ShouldBe("GET");
        _graphHandler.Requests[0].Url.ShouldContain("/v1.0/servicePrincipals");
        _graphHandler.Requests[1].Method.ShouldBe("POST");
        _graphHandler.Requests[1].Url.ShouldContain("/v1.0/users/user-42/appRoleAssignments");
        _graphHandler.Requests[1].Body.ShouldContain("r-editor");
        _graphHandler.Requests[1].Body.ShouldContain("sp-object-id-1");
    }

    [Fact]
    public async Task RemoveClientRoleAsync_FindsAssignment_ThenDeletesIt()
    {
        const string appId = "11111111-1111-1111-1111-111111111111";
        string resolveResponse = $$"""
            {"value":[{
              "id":"sp-object-id-1",
              "appId":"{{appId}}",
              "appRoles":[
                {"id":"r-editor","displayName":"Editor","value":"Editor","description":null,"isEnabled":true}
              ]
            }]}
            """;
        const string assignmentsResponse = """
            {"value":[
              {"id":"assignment-42","appRoleId":"r-editor","principalId":"user-42","resourceId":"sp-object-id-1"}
            ]}
            """;
        const string deleteResponse = "{}";
        EntraIdIdentityProvider provider = BuildProvider(resolveResponse, assignmentsResponse, deleteResponse);

        await provider.RemoveClientRoleAsync(
            "user-42", appId, "Editor", TestContext.Current.CancellationToken);

        _graphHandler!.Requests.Count.ShouldBe(3);
        _graphHandler.Requests[2].Method.ShouldBe("DELETE");
        _graphHandler.Requests[2].Url.ShouldContain("/v1.0/users/user-42/appRoleAssignments/assignment-42");
    }

    [Fact]
    public async Task RemoveClientRoleAsync_NoMatchingAssignment_Noop()
    {
        const string appId = "11111111-1111-1111-1111-111111111111";
        string resolveResponse = $$"""
            {"value":[{
              "id":"sp-object-id-1",
              "appId":"{{appId}}",
              "appRoles":[
                {"id":"r-editor","displayName":"Editor","value":"Editor","description":null,"isEnabled":true}
              ]
            }]}
            """;
        const string emptyAssignments = """{"value":[]}""";
        EntraIdIdentityProvider provider = BuildProvider(resolveResponse, emptyAssignments);

        await provider.RemoveClientRoleAsync(
            "user-42", appId, "Editor", TestContext.Current.CancellationToken);

        // No DELETE issued — only the resolve + list calls.
        _graphHandler!.Requests.Count.ShouldBe(2);
        _graphHandler.Requests.ShouldAllBe(r => r.Method != "DELETE");
    }
}
