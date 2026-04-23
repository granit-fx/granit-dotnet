using Granit.Events;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Internal;
using Granit.Identity.Federated.EntraId.Internal.Sync;
using Granit.Identity.Models;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using EntraOpts = Granit.Identity.Federated.EntraId.Options.EntraIdAdminOptions;
using SyncOpts = Granit.Identity.Federated.EntraId.Options.EntraIdClientRoleSyncOptions;

namespace Granit.Identity.Federated.EntraId.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the Entra ID client-role sync pipeline, validated
/// against an in-process WireMock.Net server standing in for Microsoft Graph. Five
/// scenarios mirror the coverage intent of the Keycloak integration suite (#1115):
/// happy path, idempotency, description drift, unknown-appId handling, and the
/// user-assignment join.
/// </summary>
public sealed class EntraIdClientRoleSyncTests : IClassFixture<EntraIdWireMockFixture>, IDisposable
{
    private readonly EntraIdWireMockFixture _wireMock;
    private readonly HttpClient _sharedHttpClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public EntraIdClientRoleSyncTests(EntraIdWireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();

        // Single HttpClient instance per test — BaseAddress points at WireMock so Graph
        // traffic lands there; the rewriting handler redirects login.microsoftonline.com
        // to the same WireMock host, so the hardcoded token endpoint is caught too.
        GraphUrlRewritingHandler rewriter = new(new Uri(_wireMock.BaseUrl))
        {
            InnerHandler = new HttpClientHandler(),
        };
        _sharedHttpClient = new HttpClient(rewriter) { BaseAddress = new Uri(_wireMock.BaseUrl) };

        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("MicrosoftGraph").Returns(_sharedHttpClient);
        _httpClientFactory = factory;
    }

    public void Dispose() => _sharedHttpClient.Dispose();

    private (EntraIdClientRoleSyncService Sync, EntraIdIdentityProvider Provider, InMemoryRoleMetadataStore Store) BuildSut(
        params string[] trackedAppIds)
    {
        EntraOpts adminOpts = new()
        {
            TenantId = EntraIdWireMockFixture.TenantId,
            ClientId = EntraIdWireMockFixture.AdminClientId,
            ClientSecret = EntraIdWireMockFixture.AdminClientSecret,
            ServicePrincipalObjectId = EntraIdWireMockFixture.ServicePrincipalObjectId,
            GraphBaseUrl = _wireMock.BaseUrl,
        };
        IOptions<EntraOpts> adminOptsWrapper = Microsoft.Extensions.Options.Options.Create(adminOpts);

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        EntraIdAdminTokenService tokenService = new(
            _httpClientFactory, adminOptsWrapper, clock,
            NullLogger<EntraIdAdminTokenService>.Instance);

        IPasswordResetNotifier notifier = Substitute.For<IPasswordResetNotifier>();
        IDistributedEventBus eventBus = Substitute.For<IDistributedEventBus>();

        EntraIdIdentityProvider provider = new(
            tokenService, _httpClientFactory, adminOptsWrapper,
            notifier, eventBus,
            NullLogger<EntraIdIdentityProvider>.Instance);

        InMemoryRoleMetadataStore store = new();
        SyncOpts syncOpts = new()
        {
            Enabled = true,
            TrackedAppIds = trackedAppIds,
        };
        EntraIdClientRoleSyncService sync = new(
            provider, store, new SimpleGuidGenerator(),
            Microsoft.Extensions.Options.Options.Create(syncOpts),
            NullLogger<EntraIdClientRoleSyncService>.Instance);

        return (sync, provider, store);
    }

    // ─── Fixture data used by every scenario ─────────────────────────────────

    private const string AppRolesOriginal = """
        [
          {"id":"r-editor","displayName":"Editor","value":"Editor","description":"Edit showcase documents","isEnabled":true},
          {"id":"r-viewer","displayName":"Viewer","value":"Viewer","description":"Read showcase documents","isEnabled":true},
          {"id":"r-admin","displayName":"Admin","value":"Admin","description":"Full showcase access","isEnabled":true},
          {"id":"r-disabled","displayName":"Legacy","value":"Legacy","description":"Disabled role","isEnabled":false}
        ]
        """;

    private const string AppRolesDescriptionChanged = """
        [
          {"id":"r-editor","displayName":"Editor","value":"Editor","description":"Edit showcase documents — updated","isEnabled":true},
          {"id":"r-viewer","displayName":"Viewer","value":"Viewer","description":"Read showcase documents","isEnabled":true},
          {"id":"r-admin","displayName":"Admin","value":"Admin","description":"Full showcase access","isEnabled":true}
        ]
        """;

    [Fact]
    public async Task SyncAsync_HappyPath_FiltersDisabledRoles_PersistsRoleMetadata()
    {
        _wireMock.StubTokenEndpoint();
        _wireMock.StubServicePrincipal(
            EntraIdWireMockFixture.TrackedAppId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            AppRolesOriginal);

        (EntraIdClientRoleSyncService sync, _, InMemoryRoleMetadataStore store) = BuildSut(EntraIdWireMockFixture.TrackedAppId);

        await sync.SyncAsync(TestContext.Current.CancellationToken);

        store.All.Count.ShouldBe(3, "the isEnabled=false role must be filtered out");
        store.All.ShouldAllBe(r => r.ClientId == EntraIdWireMockFixture.TrackedAppId);
        store.All.Select(r => r.Name).OrderBy(x => x).ShouldBe(["Admin", "Editor", "Viewer"]);
    }

    [Fact]
    public async Task SyncAsync_RerunAfterNoChanges_IsIdempotent()
    {
        _wireMock.StubTokenEndpoint();
        _wireMock.StubServicePrincipal(
            EntraIdWireMockFixture.TrackedAppId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            AppRolesOriginal);

        (EntraIdClientRoleSyncService sync, _, InMemoryRoleMetadataStore store) = BuildSut(EntraIdWireMockFixture.TrackedAppId);

        await sync.SyncAsync(TestContext.Current.CancellationToken);
        IReadOnlyList<Guid> firstRun = store.All.Select(r => r.Id).OrderBy(g => g).ToList();

        await sync.SyncAsync(TestContext.Current.CancellationToken);

        store.All.Select(r => r.Id).OrderBy(g => g).ShouldBe(firstRun);
        store.All.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SyncAsync_DescriptionChanged_UpdatesExistingRow()
    {
        _wireMock.StubTokenEndpoint();
        _wireMock.StubServicePrincipal(
            EntraIdWireMockFixture.TrackedAppId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            AppRolesOriginal);

        (EntraIdClientRoleSyncService sync, _, InMemoryRoleMetadataStore store) = BuildSut(EntraIdWireMockFixture.TrackedAppId);
        await sync.SyncAsync(TestContext.Current.CancellationToken);
        Guid originalEditorId = store.All.Single(r => r.Name == "Editor").Id;

        // Swap the servicePrincipal stub so the second GET returns the mutated description.
        _wireMock.Reset();
        _wireMock.StubTokenEndpoint();
        _wireMock.StubServicePrincipal(
            EntraIdWireMockFixture.TrackedAppId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            AppRolesDescriptionChanged);

        await sync.SyncAsync(TestContext.Current.CancellationToken);

        Granit.Authorization.Domain.RoleMetadata editor = store.All.Single(r => r.Name == "Editor");
        editor.Id.ShouldBe(originalEditorId);
        editor.Description.ShouldBe("Edit showcase documents — updated");
        store.All.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SyncAsync_UnknownAppId_LogsWarningAndContinues()
    {
        _wireMock.StubTokenEndpoint();
        _wireMock.StubServicePrincipalNotFound(EntraIdWireMockFixture.UnknownAppId);
        _wireMock.StubServicePrincipal(
            EntraIdWireMockFixture.TrackedAppId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            AppRolesOriginal);

        (EntraIdClientRoleSyncService sync, EntraIdIdentityProvider provider, InMemoryRoleMetadataStore store) = BuildSut(
            EntraIdWireMockFixture.UnknownAppId,
            EntraIdWireMockFixture.TrackedAppId);

        // Direct provider call first — confirms the exception shape for the sync.
        EntraIdClientNotFoundException ex = await Should.ThrowAsync<EntraIdClientNotFoundException>(
            async () => await provider.GetClientRolesAsync(
                EntraIdWireMockFixture.UnknownAppId, TestContext.Current.CancellationToken));
        ex.AppId.ShouldBe(EntraIdWireMockFixture.UnknownAppId);

        // Full sync: the unknown app is skipped, the tracked app still writes metadata.
        await sync.SyncAsync(TestContext.Current.CancellationToken);

        store.All.Count(r => r.ClientId == EntraIdWireMockFixture.TrackedAppId).ShouldBe(3);
        store.All.ShouldNotContain(r => r.ClientId == EntraIdWireMockFixture.UnknownAppId);
    }

    [Fact]
    public async Task GetUserClientRolesAsync_JoinsAppRoleAssignmentsWithAppRoleMap()
    {
        _wireMock.StubTokenEndpoint();
        _wireMock.StubServicePrincipal(
            EntraIdWireMockFixture.TrackedAppId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            AppRolesOriginal);
        _wireMock.StubUserAppRoleAssignments(
            EntraIdWireMockFixture.TestUserId,
            EntraIdWireMockFixture.TrackedSpObjectId,
            $$"""
            [
              {"id":"a1","appRoleId":"r-admin","principalId":"{{EntraIdWireMockFixture.TestUserId}}","resourceId":"{{EntraIdWireMockFixture.TrackedSpObjectId}}"},
              {"id":"a2","appRoleId":"r-editor","principalId":"{{EntraIdWireMockFixture.TestUserId}}","resourceId":"{{EntraIdWireMockFixture.TrackedSpObjectId}}"}
            ]
            """);

        (_, EntraIdIdentityProvider provider, _) = BuildSut();

        IReadOnlyList<IdentityRole> roles = await provider.GetUserClientRolesAsync(
            EntraIdWireMockFixture.TestUserId,
            EntraIdWireMockFixture.TrackedAppId,
            TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(2);
        roles.ShouldAllBe(r => r.ClientId == EntraIdWireMockFixture.TrackedAppId);
        roles.Select(r => r.Name).OrderBy(x => x).ShouldBe(["Admin", "Editor"]);
    }
}
