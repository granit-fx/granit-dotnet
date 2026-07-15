using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using Granit.Events;
using Granit.Guids;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Sync;
using Granit.Identity.Federated.Sync;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using AdminOpts = Granit.Identity.Federated.Cognito.Options.CognitoAdminOptions;
using SyncOpts = Granit.Identity.Federated.Cognito.Options.CognitoClientRoleSyncOptions;

namespace Granit.Identity.Federated.Cognito.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the Cognito client-role sync pipeline, validated
/// against an in-process WireMock.Net server standing in for the AWS Cognito admin
/// API. Five scenarios mirror the coverage intent of #1115 / #1116: happy path with
/// naming-prefix filtering, idempotency, description drift, forbidden-continues, and
/// the user-assignment prefix filter.
/// </summary>
public sealed class CognitoClientRoleSyncTests : IClassFixture<CognitoWireMockFixture>
{
    private readonly CognitoWireMockFixture _wireMock;

    public CognitoClientRoleSyncTests(CognitoWireMockFixture wireMock)
    {
        _wireMock = wireMock;
        _wireMock.Reset();
    }

    /// <summary>
    /// Test harness bundle: the sync service, the provider it talks to, the in-memory store,
    /// and ownership of the underlying <see cref="IAmazonCognitoIdentityProvider"/> so
    /// tests using <c>using</c> deterministically dispose the AWS SDK client. Keeping
    /// ownership explicit here avoids leaking SDK HTTP handlers between tests.
    /// </summary>
    private sealed class SutContext(
        ClientRoleSyncEngine engine,
        IClientRoleSyncPolicy policy,
        CognitoIdentityProvider provider,
        InMemoryRoleMetadataStore store,
        IAmazonCognitoIdentityProvider cognitoClient) : IDisposable
    {
        public ClientRoleSyncEngine Engine { get; } = engine;

        public IClientRoleSyncPolicy Policy { get; } = policy;

        public CognitoIdentityProvider Provider { get; } = provider;

        public InMemoryRoleMetadataStore Store { get; } = store;

        public void Dispose() => cognitoClient.Dispose();
    }

    private SutContext BuildSut(
        string delimiter = ":",
        params string[] trackedAppClientIds)
    {
        // Point the AWS SDK at WireMock via ServiceURL — SigV4 signing still happens but
        // WireMock ignores the signature; only X-Amz-Target is matched.
        AmazonCognitoIdentityProviderConfig sdkConfig = new()
        {
            ServiceURL = _wireMock.ServiceUrl,
            AuthenticationRegion = CognitoWireMockFixture.Region,
        };
        IAmazonCognitoIdentityProvider cognitoClient = new AmazonCognitoIdentityProviderClient(
            new BasicAWSCredentials("fake-access-key", "fake-secret-key"),
            sdkConfig);

        AdminOpts adminOpts = new()
        {
            Region = CognitoWireMockFixture.Region,
            UserPoolId = CognitoWireMockFixture.UserPoolId,
            AppClientId = CognitoWireMockFixture.AppClientIdA,
        };
        SyncOpts syncOpts = new()
        {
            Enabled = true,
            TrackedAppClientIds = trackedAppClientIds,
            Delimiter = delimiter,
        };

        IDistributedEventBus eventBus = Substitute.For<IDistributedEventBus>();

        CognitoIdentityProvider provider = new(
            cognitoClient,
            Microsoft.Extensions.Options.Options.Create(adminOpts),
            Microsoft.Extensions.Options.Options.Create(syncOpts),
            eventBus,
            NullLogger<CognitoIdentityProvider>.Instance);

        InMemoryRoleMetadataStore store = new();
        Granit.Timing.IClock clock = Substitute.For<Granit.Timing.IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);

        ClientRoleSyncEngine engine = new(
            provider, store, new SimpleGuidGenerator(), clock,
            NullLogger<ClientRoleSyncEngine>.Instance);
        CognitoClientRoleSyncPolicy policy = new(Microsoft.Extensions.Options.Options.Create(syncOpts));

        return new SutContext(engine, policy, provider, store, cognitoClient);
    }

    [Fact]
    public async Task SyncAsync_HappyPath_PersistsRoleMetadataWithClientId_ForPrefixedGroupsOnly()
    {
        _wireMock.StubListGroups(
            ("clientA:editor", "Edit showcase documents"),
            ("clientA:viewer", "Read showcase documents"),
            ("clientA:admin", "Full showcase access"),
            ("clientB:admin", "Not in scope"),
            ("platform-admins", "Realm-only group"));

        using SutContext sut = BuildSut(trackedAppClientIds: CognitoWireMockFixture.AppClientIdA);

        await sut.Engine.SyncAsync(sut.Policy, TestContext.Current.CancellationToken);

        sut.Store.All.Count.ShouldBe(3, "only the 3 clientA-prefixed groups should be synced");
        sut.Store.All.ShouldAllBe(r => r.ClientId == CognitoWireMockFixture.AppClientIdA);
        sut.Store.All.Select(r => r.Name).Order().ShouldBe(["admin", "editor", "viewer"]);
        sut.Store.All.First(r => r.Name == "editor").Description.ShouldBe("Edit showcase documents");
    }

    [Fact]
    public async Task SyncAsync_Idempotent_SecondRunNoOp()
    {
        _wireMock.StubListGroups(
            ("clientA:editor", "Edit showcase documents"),
            ("clientA:viewer", "Read showcase documents"));

        using SutContext sut = BuildSut(trackedAppClientIds: CognitoWireMockFixture.AppClientIdA);

        await sut.Engine.SyncAsync(sut.Policy, TestContext.Current.CancellationToken);
        IReadOnlyList<Guid> firstRun = sut.Store.All.Select(r => r.Id).Order().ToList();

        await sut.Engine.SyncAsync(sut.Policy, TestContext.Current.CancellationToken);

        sut.Store.All.Select(r => r.Id).Order().ShouldBe(firstRun);
        sut.Store.All.Count.ShouldBe(2);
    }

    [Fact]
    public async Task SyncAsync_DescriptionChanged_UpdatesExistingRow()
    {
        _wireMock.StubListGroups(
            ("clientA:editor", "Edit showcase documents"));

        using SutContext sut = BuildSut(trackedAppClientIds: CognitoWireMockFixture.AppClientIdA);

        await sut.Engine.SyncAsync(sut.Policy, TestContext.Current.CancellationToken);
        Guid originalId = sut.Store.All.Single(r => r.Name == "editor").Id;

        // Swap the stub to return a mutated description.
        _wireMock.Reset();
        _wireMock.StubListGroups(
            ("clientA:editor", "Edit showcase documents — updated"));

        await sut.Engine.SyncAsync(sut.Policy, TestContext.Current.CancellationToken);

        Granit.Authorization.Domain.RoleMetadata editor = sut.Store.All.Single(r => r.Name == "editor");
        editor.Id.ShouldBe(originalId);
        editor.Description.ShouldBe("Edit showcase documents — updated");
        sut.Store.All.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SyncAsync_NotAuthorized_LogsErrorAndContinues()
    {
        _wireMock.StubListGroupsNotAuthorized();

        using SutContext sut = BuildSut(trackedAppClientIds: CognitoWireMockFixture.AppClientIdA);

        // Sync must not throw — the service logs the 403-equivalent and carries on.
        await sut.Engine.SyncAsync(sut.Policy, TestContext.Current.CancellationToken);

        sut.Store.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserClientRolesAsync_FiltersToPrefixedGroups()
    {
        _wireMock.StubAdminListGroupsForUser(
            ("clientA:admin", null),
            ("clientA:editor", null),
            ("platform-admins", "Realm-only"));

        using SutContext sut = BuildSut(trackedAppClientIds: CognitoWireMockFixture.AppClientIdA);

        IReadOnlyList<IdentityRole> roles = await sut.Provider.GetUserClientRolesAsync(
            userId: "alice",
            clientId: CognitoWireMockFixture.AppClientIdA,
            cancellationToken: TestContext.Current.CancellationToken);

        roles.Count.ShouldBe(2);
        roles.ShouldAllBe(r => r.ClientId == CognitoWireMockFixture.AppClientIdA);
        roles.Select(r => r.Name).Order().ShouldBe(["admin", "editor"]);
    }
}
