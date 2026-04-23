using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Federated.Cognito.Internal.Sync;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using CgSyncOpts = Granit.Identity.Federated.Cognito.Options.CognitoClientRoleSyncOptions;

namespace Granit.Identity.Federated.Cognito.Tests.Sync;

public sealed class CognitoClientRoleSyncServiceTests
{
    private readonly IIdentityClientRoleManager _clientRoleManager =
        Substitute.For<IIdentityClientRoleManager>();
    private readonly IRoleMetadataStore _store = Substitute.For<IRoleMetadataStore>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    public CognitoClientRoleSyncServiceTests()
    {
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

    private CognitoClientRoleSyncService BuildSut(params string[] trackedAppClientIds)
    {
        CgSyncOpts opts = new()
        {
            Enabled = true,
            TrackedAppClientIds = trackedAppClientIds,
        };
        return new CognitoClientRoleSyncService(
            _clientRoleManager, _store, _guidGenerator,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<CognitoClientRoleSyncService>.Instance);
    }

    [Fact]
    public async Task SyncAsync_Disabled_NoCall()
    {
        CgSyncOpts opts = new() { Enabled = false, TrackedAppClientIds = ["client-a"] };
        CognitoClientRoleSyncService sut = new(
            _clientRoleManager, _store, _guidGenerator,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<CognitoClientRoleSyncService>.Instance);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _clientRoleManager.DidNotReceive().GetClientRolesAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_HappyPath_InsertsNewRoles()
    {
        CognitoClientRoleSyncService sut = BuildSut("client-a");
        _clientRoleManager.GetClientRolesAsync("client-a", Arg.Any<CancellationToken>())
            .Returns([
                new IdentityRole("client-a:editor", "editor", "Edit docs") { ClientId = "client-a" },
                new IdentityRole("client-a:viewer", "viewer", null) { ClientId = "client-a" },
            ]);
        _store.FindByNameAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>()).Returns((RoleMetadata?)null);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(2).AddAsync(
            Arg.Is<RoleMetadata>(r => r.ClientId == "client-a"
                && r.MultiTenancySide == MultiTenancySide.Host
                && !r.IsSystem && r.TenantId == null),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_Idempotent_SecondRunNoOpsWhenUnchanged()
    {
        CognitoClientRoleSyncService sut = BuildSut("client-a");
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySide.Host,
            tenantId: null, clientId: "client-a", description: "Edit docs");
        _clientRoleManager.GetClientRolesAsync("client-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("client-a:editor", "editor", "Edit docs") { ClientId = "client-a" }]);
        _store.FindByNameAsync("editor", null, "client-a", Arg.Any<CancellationToken>())
            .Returns(existing);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
        await _store.DidNotReceive().UpdateAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_DescriptionDrift_CallsUpdate()
    {
        CognitoClientRoleSyncService sut = BuildSut("client-a");
        var existing = RoleMetadata.Create(
            Guid.NewGuid(), "editor", MultiTenancySide.Host,
            tenantId: null, clientId: "client-a", description: "OLD description");
        _clientRoleManager.GetClientRolesAsync("client-a", Arg.Any<CancellationToken>())
            .Returns([new IdentityRole("client-a:editor", "editor", "NEW description") { ClientId = "client-a" }]);
        _store.FindByNameAsync("editor", null, "client-a", Arg.Any<CancellationToken>())
            .Returns(existing);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _store.Received(1).UpdateAsync(
            Arg.Is<RoleMetadata>(r => r.Description == "NEW description"),
            Arg.Any<CancellationToken>());
        await _store.DidNotReceive().AddAsync(Arg.Any<RoleMetadata>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAsync_FailureOnFirstClient_LogsAndContinuesToSecond()
    {
        CognitoClientRoleSyncService sut = BuildSut("broken", "client-b");
        _clientRoleManager.GetClientRolesAsync("broken", Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<IdentityRole>>>(_ =>
                throw new Amazon.CognitoIdentityProvider.Model.NotAuthorizedException("denied"));
        _clientRoleManager.GetClientRolesAsync("client-b", Arg.Any<CancellationToken>())
            .Returns([]);

        await sut.SyncAsync(TestContext.Current.CancellationToken);

        await _clientRoleManager.Received(1).GetClientRolesAsync("client-b", Arg.Any<CancellationToken>());
    }
}
