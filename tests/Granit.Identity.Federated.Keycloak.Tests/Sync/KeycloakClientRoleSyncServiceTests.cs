using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.Keycloak.Exceptions;
using Granit.Identity.Federated.Keycloak.Internal.Sync;
using Granit.Identity.Models;
using Granit.MultiTenancy;
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

    public KeycloakClientRoleSyncServiceTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

    private KeycloakClientRoleSyncService BuildSut(params string[] trackedClientIds)
    {
        KcSyncOpts opts = new()
        {
            Enabled = true,
            TrackedClientIds = trackedClientIds,
        };
        return new KeycloakClientRoleSyncService(
            _clientRoleManager, _store, _guidGenerator,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<KeycloakClientRoleSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_Disabled_NoCall()
    {
        KcSyncOpts opts = new() { Enabled = false, TrackedClientIds = ["app-a"] };
        KeycloakClientRoleSyncService sut = new(
            _clientRoleManager, _store, _guidGenerator, Microsoft.Extensions.Options.Options.Create(opts),
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
}
