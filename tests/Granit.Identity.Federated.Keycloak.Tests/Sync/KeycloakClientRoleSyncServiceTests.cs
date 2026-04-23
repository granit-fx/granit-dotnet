using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Internal.Sync;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using KcSyncOpts = Granit.Identity.Federated.Keycloak.Options.KeycloakClientRoleSyncOptions;

namespace Granit.Identity.Federated.Keycloak.Tests.Sync;

public sealed class KeycloakClientRoleSyncServiceTests
{
    private readonly IIdentityClientRoleManager _clientRoleManager =
        Substitute.For<IIdentityClientRoleManager>();
    private readonly IRoleMetadataStore _store = Substitute.For<IRoleMetadataStore>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public KeycloakClientRoleSyncServiceTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _clock.Now.Returns(new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero));
        _store.ListByClientIdAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<RoleMetadata>>(_ => []);
    }

    private KeycloakClientRoleSyncService BuildSut(params string[] trackedClientIds) =>
        BuildSut(OrphanedRolePolicy.KeepAndLog, trackedClientIds);

    private KeycloakClientRoleSyncService BuildSut(OrphanedRolePolicy policy, params string[] trackedClientIds)
    {
        KcSyncOpts opts = new()
        {
            Enabled = true,
            TrackedClientIds = trackedClientIds,
            OrphanedRolePolicy = policy,
        };
        return new KeycloakClientRoleSyncService(
            _clientRoleManager, _store, _guidGenerator, _clock,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<KeycloakClientRoleSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_Disabled_NoCall()
    {
        KcSyncOpts opts = new() { Enabled = false, TrackedClientIds = ["app-a"] };
        KeycloakClientRoleSyncService sut = new(
            _clientRoleManager, _store, _guidGenerator, _clock,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<KeycloakClientRoleSyncService>.Instance);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _clientRoleManager.DidNotReceive().GetClientRolesAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HappyPath_InsertsNewRoles()
    {
        KeycloakClientRoleSyncService sut = BuildSut("app-a");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([
                new IdentityRole("r1", "editor", "Edit docs") { ClientId = "app-a" },
                new IdentityRole("r2", "viewer", null) { ClientId = "app-a" },
            ]);
        _store.FindByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(2).AddAsync(
            Arg.Is<RoleMetadata>(r => r.ClientId == "app-a" && r.MultiTenancySide == MultiTenancySide.Host
                && !r.IsSystem && r.TenantId == null),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_Idempotent_SecondRunNoOpsWhenUnchanged()
    {
        KeycloakClientRoleSyncService sut = BuildSut("app-a");
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a", description: "Edit docs");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "editor", "Edit docs") { ClientId = "app-a" }]);
        _store.FindByNameAsync("editor", null, "app-a", Arg.Any<CancellationToken>())
            .Returns(existing);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DescriptionDrift_CallsUpdate()
    {
        KeycloakClientRoleSyncService sut = BuildSut("app-a");
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a", description: "OLD description");
        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "editor", "NEW description") { ClientId = "app-a" }]);
        _store.FindByNameAsync("editor", null, "app-a", Arg.Any<CancellationToken>())
            .Returns(existing);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.Description == "NEW description"),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_ClientNotFound_LogsAndContinues()
    {
        KeycloakClientRoleSyncService sut = BuildSut("missing", "app-b");
        _clientRoleManager.GetClientRolesAsync("missing", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<IdentityRole>>>(_ => throw new KeycloakClientNotFoundException("missing"));
        _clientRoleManager.GetClientRolesAsync("app-b", Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        // Second client still queried despite first one throwing.
        await _clientRoleManager.Received(1).GetClientRolesAsync("app-b", Arg.Any<CancellationToken>());
    }

    // ──── ADR-029 — orphan cleanup policy ────────────────────────────────

    [Fact]
    public async Task SyncAsync_KeepAndLog_ExistingRowNotReturnedByProvider_RowSurvives()
    {
        KeycloakClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.KeepAndLog, "app-a");
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a");

        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([]);  // provider no longer returns the role
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        // KeepAndLog: no mutation on the row; it stays with IsOrphaned = false.
        orphan.IsOrphaned.ShouldBeFalse();
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().RemoveAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SoftDelete_ExistingRowNotReturnedByProvider_MarkedOrphaned()
    {
        KeycloakClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.SoftDelete, "app-a");
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a");

        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        orphan.IsOrphaned.ShouldBeTrue();
        orphan.OrphanedAt.ShouldBe(_clock.Now);
        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.IsOrphaned), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().RemoveAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SoftDelete_AlreadyOrphaned_NoAdditionalWrite()
    {
        KeycloakClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.SoftDelete, "app-a");
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a");
        orphan.MarkAsOrphaned(DateTimeOffset.UtcNow.AddDays(-3));

        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HardDelete_ExistingRowNotReturnedByProvider_Removed()
    {
        KeycloakClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.HardDelete, "app-a");
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "gone", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a");

        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([]);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).RemoveAsync(orphan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_RestoreOnReturn_OrphanedRowReturnedAgain_ClearsFlag()
    {
        KeycloakClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.SoftDelete, "app-a");
        var previouslyOrphaned = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySide.Host,
            tenantId: null, clientId: "app-a", description: "Edit docs");
        previouslyOrphaned.MarkAsOrphaned(DateTimeOffset.UtcNow.AddHours(-1));

        _clientRoleManager.GetClientRolesAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "editor", "Edit docs") { ClientId = "app-a" }]);
        _store.FindByNameAsync("editor", null, "app-a", Arg.Any<CancellationToken>())
            .Returns(previouslyOrphaned);
        _store.ListByClientIdAsync("app-a", Arg.Any<CancellationToken>())
            .Returns([previouslyOrphaned]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        previouslyOrphaned.IsOrphaned.ShouldBeFalse();
        previouslyOrphaned.OrphanedAt.ShouldBeNull();
        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => !r.IsOrphaned && r.Name == "editor"),
            Arg.Any<CancellationToken>());
    }
}
