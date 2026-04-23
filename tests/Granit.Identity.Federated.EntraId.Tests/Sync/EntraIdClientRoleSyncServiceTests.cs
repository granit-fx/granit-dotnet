using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Sync;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using EntraSyncOpts = Granit.Identity.Federated.EntraId.Options.EntraIdClientRoleSyncOptions;

namespace Granit.Identity.Federated.EntraId.Tests.Sync;

public sealed class EntraIdClientRoleSyncServiceTests
{
    private readonly IIdentityClientRoleManager _clientRoleManager =
        Substitute.For<IIdentityClientRoleManager>();
    private readonly IRoleMetadataStore _store = Substitute.For<IRoleMetadataStore>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public EntraIdClientRoleSyncServiceTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
        _clock.Now.Returns(new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero));
        _store.ListByClientIdAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<RoleMetadata>>(_ => []);
    }

    private EntraIdClientRoleSyncService BuildSut(params string[] trackedAppIds) =>
        BuildSut(OrphanedRolePolicy.KeepAndLog, trackedAppIds);

    private EntraIdClientRoleSyncService BuildSut(OrphanedRolePolicy policy, params string[] trackedAppIds)
    {
        EntraSyncOpts opts = new()
        {
            Enabled = true,
            TrackedAppIds = trackedAppIds,
            OrphanedRolePolicy = policy,
        };
        return new EntraIdClientRoleSyncService(
            _clientRoleManager, _store, _guidGenerator, _clock,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<EntraIdClientRoleSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_Disabled_NoCall()
    {
        EntraSyncOpts opts = new() { Enabled = false, TrackedAppIds = ["11111111-1111-1111-1111-111111111111"] };
        EntraIdClientRoleSyncService sut = new(
            _clientRoleManager, _store, _guidGenerator, _clock,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<EntraIdClientRoleSyncService>.Instance);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _clientRoleManager.DidNotReceive().GetClientRolesAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HappyPath_InsertsNewRoles()
    {
        const string appId = "11111111-1111-1111-1111-111111111111";
        EntraIdClientRoleSyncService sut = BuildSut(appId);
        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>())
            .Returns([
                new IdentityRole("r1", "Editor", "Edit docs") { ClientId = appId },
                new IdentityRole("r2", "Viewer", null) { ClientId = appId },
            ]);
        _store.FindByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(2).AddAsync(
            Arg.Is<RoleMetadata>(r => r.ClientId == appId && r.MultiTenancySide == MultiTenancySide.Host
                && !r.IsSystem && r.TenantId == null),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_Idempotent_SecondRunNoOpsWhenUnchanged()
    {
        const string appId = "22222222-2222-2222-2222-222222222222";
        EntraIdClientRoleSyncService sut = BuildSut(appId);
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "Editor", MultiTenancySide.Host,
            tenantId: null, clientId: appId, description: "Edit docs");
        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "Editor", "Edit docs") { ClientId = appId }]);
        _store.FindByNameAsync("Editor", null, appId, Arg.Any<CancellationToken>())
            .Returns(existing);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DescriptionDrift_CallsUpdate()
    {
        const string appId = "33333333-3333-3333-3333-333333333333";
        EntraIdClientRoleSyncService sut = BuildSut(appId);
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "Editor", MultiTenancySide.Host,
            tenantId: null, clientId: appId, description: "OLD description");
        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "Editor", "NEW description") { ClientId = appId }]);
        _store.FindByNameAsync("Editor", null, appId, Arg.Any<CancellationToken>())
            .Returns(existing);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.Description == "NEW description"),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_AppNotFound_LogsAndContinues()
    {
        const string missing = "00000000-0000-0000-0000-000000000000";
        const string appB = "44444444-4444-4444-4444-444444444444";
        EntraIdClientRoleSyncService sut = BuildSut(missing, appB);
        _clientRoleManager.GetClientRolesAsync(missing, Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<IdentityRole>>>(_ => throw new EntraIdClientNotFoundException(missing));
        _clientRoleManager.GetClientRolesAsync(appB, Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        // Second app still queried despite first one throwing.
        await _clientRoleManager.Received(1).GetClientRolesAsync(appB, Arg.Any<CancellationToken>());
    }

    // ──── ADR-029 — orphan cleanup policy ────────────────────────────────

    [Fact]
    public async Task SyncAsync_KeepAndLog_ExistingRowNotReturnedByProvider_RowSurvives()
    {
        const string appId = "55555555-5555-5555-5555-555555555555";
        EntraIdClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.KeepAndLog, appId);
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "Gone", MultiTenancySide.Host,
            tenantId: null, clientId: appId);

        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync(appId, Arg.Any<CancellationToken>()).Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        orphan.IsOrphaned.ShouldBeFalse();
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().RemoveAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SoftDelete_ExistingRowNotReturnedByProvider_MarkedOrphaned()
    {
        const string appId = "66666666-6666-6666-6666-666666666666";
        EntraIdClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.SoftDelete, appId);
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "Gone", MultiTenancySide.Host,
            tenantId: null, clientId: appId);

        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync(appId, Arg.Any<CancellationToken>()).Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        orphan.IsOrphaned.ShouldBeTrue();
        orphan.OrphanedAt.ShouldBe(_clock.Now);
        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.IsOrphaned), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_SoftDelete_AlreadyOrphaned_NoAdditionalWrite()
    {
        const string appId = "77777777-7777-7777-7777-777777777777";
        EntraIdClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.SoftDelete, appId);
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "Gone", MultiTenancySide.Host,
            tenantId: null, clientId: appId);
        orphan.MarkAsOrphaned(DateTimeOffset.UtcNow.AddDays(-3));

        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync(appId, Arg.Any<CancellationToken>()).Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HardDelete_ExistingRowNotReturnedByProvider_Removed()
    {
        const string appId = "88888888-8888-8888-8888-888888888888";
        EntraIdClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.HardDelete, appId);
        var orphan = RoleMetadata.Create(
            Guid.NewGuid(), "Gone", MultiTenancySide.Host,
            tenantId: null, clientId: appId);

        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>()).Returns([]);
        _store.ListByClientIdAsync(appId, Arg.Any<CancellationToken>()).Returns([orphan]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).RemoveAsync(orphan, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_RestoreOnReturn_OrphanedRowReturnedAgain_ClearsFlag()
    {
        const string appId = "99999999-9999-9999-9999-999999999999";
        EntraIdClientRoleSyncService sut = BuildSut(OrphanedRolePolicy.SoftDelete, appId);
        var previouslyOrphaned = RoleMetadata.Create(
            Guid.NewGuid(), "Editor", MultiTenancySide.Host,
            tenantId: null, clientId: appId, description: "Edit docs");
        previouslyOrphaned.MarkAsOrphaned(DateTimeOffset.UtcNow.AddHours(-1));

        _clientRoleManager.GetClientRolesAsync(appId, Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("r1", "Editor", "Edit docs") { ClientId = appId }]);
        _store.FindByNameAsync("Editor", null, appId, Arg.Any<CancellationToken>())
            .Returns(previouslyOrphaned);
        _store.ListByClientIdAsync(appId, Arg.Any<CancellationToken>()).Returns([previouslyOrphaned]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        previouslyOrphaned.IsOrphaned.ShouldBeFalse();
        previouslyOrphaned.OrphanedAt.ShouldBeNull();
        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => !r.IsOrphaned && r.Name == "Editor"),
            Arg.Any<CancellationToken>());
    }
}
