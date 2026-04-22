using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.EntraId.Exceptions;
using Granit.Identity.Federated.EntraId.Internal.Sync;
using Granit.Identity.Models;
using Granit.MultiTenancy;
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

    public EntraIdClientRoleSyncServiceTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

    private EntraIdClientRoleSyncService BuildSut(params string[] trackedAppIds)
    {
        EntraSyncOpts opts = new()
        {
            Enabled = true,
            TrackedAppIds = trackedAppIds,
        };
        return new EntraIdClientRoleSyncService(
            _clientRoleManager, _store, _guidGenerator,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<EntraIdClientRoleSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_Disabled_NoCall()
    {
        EntraSyncOpts opts = new() { Enabled = false, TrackedAppIds = ["11111111-1111-1111-1111-111111111111"] };
        EntraIdClientRoleSyncService sut = new(
            _clientRoleManager, _store, _guidGenerator, Microsoft.Extensions.Options.Options.Create(opts),
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
}
