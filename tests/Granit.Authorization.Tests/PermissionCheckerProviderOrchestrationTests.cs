// =============================================================================
// Tests — PermissionChecker multi-provider orchestration
// =============================================================================
// These tests exercise the U → R → C provider chain:
//   - A user-level grant short-circuits role and client lookups
//   - A role-level grant short-circuits the client lookup
//   - A client-level grant is consulted last
//   - Side enforcement runs before any provider lookup
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Authorization.Cache;
using Granit.Authorization.Diagnostics;
using Granit.Authorization.Options;
using Granit.Authorization.Services;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Tests;

public sealed class PermissionCheckerProviderOrchestrationTests
{
    private const string Permission = "Invoices.Delete";

    [Fact]
    public async Task UserGrant_ShortCircuitsRoleAndClient()
    {
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("U", "alice", Permission, null, Arg.Any<CancellationToken>()).Returns(true);

        PermissionChecker checker = BuildChecker(
            userId: "alice", roles: ["editor"], clientId: "spa", store: store);

        bool granted = await checker.IsGrantedAsync(Permission, TestContext.Current.CancellationToken);

        granted.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            "R", Arg.Any<string>(), Permission, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().IsGrantedAsync(
            "C", Arg.Any<string>(), Permission, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoUserGrant_RoleGrant_StopsBeforeClient()
    {
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("U", "alice", Permission, null, Arg.Any<CancellationToken>()).Returns(false);
        store.IsGrantedAsync("R", "editor", Permission, null, Arg.Any<CancellationToken>()).Returns(true);

        PermissionChecker checker = BuildChecker(
            userId: "alice", roles: ["editor"], clientId: "spa", store: store);

        bool granted = await checker.IsGrantedAsync(Permission, TestContext.Current.CancellationToken);

        granted.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            "C", Arg.Any<string>(), Permission, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoUserNorRoleGrant_ClientGrant_Resolves()
    {
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync(Arg.Any<string>(), Arg.Any<string>(), Permission, null, Arg.Any<CancellationToken>())
            .Returns(false);
        store.IsGrantedAsync("C", "m2m-worker", Permission, null, Arg.Any<CancellationToken>()).Returns(true);

        PermissionChecker checker = BuildChecker(
            userId: "alice", roles: ["editor"], clientId: "m2m-worker", store: store);

        bool granted = await checker.IsGrantedAsync(Permission, TestContext.Current.CancellationToken);

        granted.ShouldBeTrue();
    }

    [Fact]
    public async Task NoProviderGrants_ReturnsFalse()
    {
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(false);

        PermissionChecker checker = BuildChecker(
            userId: "alice", roles: ["editor"], clientId: "spa", store: store);

        bool granted = await checker.IsGrantedAsync(Permission, TestContext.Current.CancellationToken);

        granted.ShouldBeFalse();
    }

    [Fact]
    public async Task AnonymousClient_OnlyClientKeyIsConsulted()
    {
        // Machine-to-machine: no user, no roles, only client_id. Only the client provider yields keys.
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        store.IsGrantedAsync("C", "m2m-worker", Permission, null, Arg.Any<CancellationToken>()).Returns(true);

        PermissionChecker checker = BuildChecker(
            userId: null, roles: [], clientId: "m2m-worker", store: store);

        bool granted = await checker.IsGrantedAsync(Permission, TestContext.Current.CancellationToken);

        granted.ShouldBeTrue();
        await store.DidNotReceive().IsGrantedAsync(
            "U", Arg.Any<string>(), Permission, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().IsGrantedAsync(
            "R", Arg.Any<string>(), Permission, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SideMismatch_NoProviderConsulted()
    {
        // Host-only permission checked under an active tenant: should deny without hitting the store.
        IPermissionGrantStore store = Substitute.For<IPermissionGrantStore>();
        PermissionDefinition definition = new(Permission, null, "TestGroup", MultiTenancySide.Host);

        PermissionChecker checker = BuildChecker(
            userId: "alice", roles: ["editor"], clientId: "spa",
            store: store,
            tenantId: Guid.NewGuid(),
            definition: definition);

        bool granted = await checker.IsGrantedAsync(Permission, TestContext.Current.CancellationToken);

        granted.ShouldBeFalse();
        await store.DidNotReceive().IsGrantedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    // --- Helpers ---

    private static PermissionChecker BuildChecker(
        string? userId,
        IReadOnlyList<string> roles,
        string? clientId,
        IPermissionGrantStore store,
        Guid? tenantId = null,
        PermissionDefinition? definition = null)
    {
        ICurrentUserService user = Substitute.For<ICurrentUserService>();
        user.IsAuthenticated.Returns(true);
        user.UserId.Returns(userId);
        user.GetRoles().Returns(roles);
        user.ClientId.Returns(clientId);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(tenantId.HasValue);
        tenant.Id.Returns(tenantId);

        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists(Permission).Returns(true);
        manager.Find(Permission).Returns(definition
            ?? new PermissionDefinition(Permission, null, "TestGroup", MultiTenancySide.Both));

        IFusionCache cache = new FusionCache(new FusionCacheOptions());

        GranitAuthorizationOptions opts = new()
        {
            AdminRoles = ["admin"],
            CacheDuration = TimeSpan.FromMinutes(5),
        };

        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        AuthorizationMetrics metrics = new(meterFactory);

        IEnumerable<IPermissionGrantProvider> providers =
        [
            new UserPermissionGrantProvider(),
            new RolePermissionGrantProvider(),
            new ClientPermissionGrantProvider(),
        ];

        return new PermissionChecker(
            user, tenant, manager, store, providers, cache, metrics,
            Microsoft.Extensions.Options.Options.Create(opts));
    }
}
