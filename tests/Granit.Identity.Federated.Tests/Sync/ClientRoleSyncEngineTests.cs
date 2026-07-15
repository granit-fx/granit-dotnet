using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity.Federated.Sync;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Tests.Sync;

/// <summary>
/// The provider-agnostic client-role sync logic (ADR-029/030) shared by every federated provider.
/// These tests moved here from the per-provider *ClientRoleSyncService tests when the logic was
/// extracted into <see cref="ClientRoleSyncEngine"/>; the per-provider slice (which client ids,
/// how to classify exceptions) is now an <see cref="IClientRoleSyncPolicy"/> and is exercised by a
/// stub here and by the thin provider policy tests.
/// </summary>
public sealed class ClientRoleSyncEngineTests
{
    private readonly IIdentityClientRoleManager _clientRoleManager =
        Substitute.For<IIdentityClientRoleManager>();
    private readonly IRoleMetadataStore _store = Substitute.For<IRoleMetadataStore>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public ClientRoleSyncEngineTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _clock.Now.Returns(new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero));
        _store.ListByClientIdAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<RoleMetadata>>(_ => []);
    }

    private ClientRoleSyncEngine BuildEngine() => new(
        _clientRoleManager, _store, _guidGenerator, _clock,
        NullLogger<ClientRoleSyncEngine>.Instance);

    private static StubPolicy Policy(params string[] trackedClientIds) =>
        new() { TrackedClientIds = trackedClientIds };

    private static StubPolicy Policy(OrphanedRolePolicy orphanPolicy, params string[] trackedClientIds) =>
        new() { OrphanedRolePolicy = orphanPolicy, TrackedClientIds = trackedClientIds };

    private Task Sync(IClientRoleSyncPolicy policy) =>
        BuildEngine().SyncAsync(policy, TestContext.Current.CancellationToken);

    [Fact]
    public async Task SyncAsync_Disabled_NoCall()
    {
        await Sync(new StubPolicy { Enabled = false, TrackedClientIds = ["app-a"] });

        await _clientRoleManager.DidNotReceive().GetClientRolesAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_NoTrackedClients_NoCall()
    {
        await Sync(new StubPolicy { Enabled = true, TrackedClientIds = [] });

        await _clientRoleManager.DidNotReceive().GetClientRolesAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HappyPath_InsertsNewRoles()
    {
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([
                new IdentityRole("r1", "editor", "Edit docs") { ClientId = "app-a" },
                new IdentityRole("r2", "viewer", null) { ClientId = "app-a" },
            ]);
        _store.FindByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        await Sync(Policy("app-a"));

        await _store.Received(2).AddAsync(
            Arg.Is<RoleMetadata>(r => r.ClientId == "app-a" && r.MultiTenancySides == MultiTenancySides.Host
                && !r.IsSystem && r.TenantId == null),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_Idempotent_SecondRunNoOpsWhenUnchanged()
    {
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySides.Host,
            tenantId: null, clientId: "app-a", description: "Edit docs");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "editor", "Edit docs") { ClientId = "app-a" }]);
        _store.FindByNameAsync("editor", null, "app-a", Arg.Any<CancellationToken>())
            .Returns(existing);

        await Sync(Policy("app-a"));

        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DescriptionDrift_CallsUpdate()
    {
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySides.Host,
            tenantId: null, clientId: "app-a", description: "OLD description");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "editor", "NEW description") { ClientId = "app-a" }]);
        _store.FindByNameAsync("editor", null, "app-a", Arg.Any<CancellationToken>())
            .Returns(existing);

        await Sync(Policy("app-a"));

        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.Description == "NEW description"),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ClassifiedFetchFault_LogsAndContinues()
    {
        _clientRoleManager.GetClientRolesAsync("missing", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<IdentityRole>>>(_ => throw new TestClientNotFoundException());
        _clientRoleManager.GetClientRolesAsync("app-b", Arg.Any<CancellationToken>())
            .Returns([]);

        await Sync(Policy("missing", "app-b"));

        // The classified fault is swallowed; the next client is still queried.
        await _clientRoleManager.Received(1).GetClientRolesAsync("app-b", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_UnclassifiedException_Propagates()
    {
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<IdentityRole>>>(_ => throw new InvalidOperationException("boom"));

        // A policy that classifies nothing lets the exception surface (unexpected fault).
        await Should.ThrowAsync<InvalidOperationException>(() => Sync(Policy("app-a")));
    }

    // ──── ADR-029 — orphan cleanup policy ────────────────────────────────

    [Fact]
    public async Task SyncAsync_KeepAndLog_ExistingRowNotReturnedByProvider_RowSurvives()
    {
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySides.Host, tenantId: null, clientId: "app-a");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>()).Returns([orphan]);

        await Sync(Policy(OrphanedRolePolicy.KeepAndLog, "app-a"));

        orphan.IsOrphaned.ShouldBeFalse();
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().RemoveAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SoftDelete_ExistingRowNotReturnedByProvider_MarkedOrphaned()
    {
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySides.Host, tenantId: null, clientId: "app-a");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>()).Returns([orphan]);

        await Sync(Policy(OrphanedRolePolicy.SoftDelete, "app-a"));

        orphan.IsOrphaned.ShouldBeTrue();
        orphan.OrphanedAt.ShouldBe(_clock.Now);
        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.IsOrphaned), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().RemoveAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SoftDelete_AlreadyOrphaned_NoAdditionalWrite()
    {
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySides.Host, tenantId: null, clientId: "app-a");
        orphan.MarkAsOrphaned(DateTimeOffset.UtcNow.AddDays(-3));
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>()).Returns([orphan]);

        await Sync(Policy(OrphanedRolePolicy.SoftDelete, "app-a"));

        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HardDelete_ExistingRowNotReturnedByProvider_Removed()
    {
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySides.Host, tenantId: null, clientId: "app-a");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>()).Returns([orphan]);

        await Sync(Policy(OrphanedRolePolicy.HardDelete, "app-a"));

        await _store.Received(1).RemoveAsync(orphan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_RestoreOnReturn_OrphanedRowReturnedAgain_ClearsFlag()
    {
        var previouslyOrphaned = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySides.Host,
            tenantId: null, clientId: "app-a", description: "Edit docs");
        previouslyOrphaned.MarkAsOrphaned(DateTimeOffset.UtcNow.AddHours(-1));
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "editor", "Edit docs") { ClientId = "app-a" }]);
        _store.FindByNameAsync("editor", null, "app-a", Arg.Any<CancellationToken>())
            .Returns(previouslyOrphaned);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([previouslyOrphaned]);

        await Sync(Policy(OrphanedRolePolicy.SoftDelete, "app-a"));

        previouslyOrphaned.IsOrphaned.ShouldBeFalse();
        previouslyOrphaned.OrphanedAt.ShouldBeNull();
        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => !r.IsOrphaned && r.Name == "editor"),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private sealed class TestClientNotFoundException() : Exception("client not found");

    private sealed class StubPolicy : IClientRoleSyncPolicy
    {
        public string ProviderName => "Test";
        public bool Enabled { get; init; } = true;
        public IReadOnlyList<string> TrackedClientIds { get; init; } = [];
        public OrphanedRolePolicy OrphanedRolePolicy { get; init; } = OrphanedRolePolicy.KeepAndLog;
        public string ForbiddenRemediation => string.Empty;

        public ClientRoleFetchFault? ClassifyFetchFault(Exception exception) =>
            exception is TestClientNotFoundException ? ClientRoleFetchFault.ClientNotFound : null;
    }
}
