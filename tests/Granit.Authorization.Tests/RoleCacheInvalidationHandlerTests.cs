using Granit.Authorization.Cache;
using Granit.Authorization.Events;
using Granit.Authorization.Services;
using Granit.MultiTenancy;
using NSubstitute;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Tests;

/// <summary>
/// Verifies that <see cref="RoleCacheInvalidationHandler"/> flushes stale
/// permission-check cache entries when a role is renamed or deleted. The
/// handler relies on <see cref="PermissionChecker.RoleTag"/> being applied to
/// role-scope cache entries at set time (covered separately in
/// <see cref="PermissionCheckerCacheTagTests"/>).
/// </summary>
public sealed class RoleCacheInvalidationHandlerTests
{
    [Fact]
    public async Task RoleUpdated_Renamed_RemovesByPreviousNameTag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new RoleUpdatedEvent(
            RoleId: Guid.NewGuid(),
            Name: "TeamLead",
            PreviousName: "Manager",
            MultiTenancySides: MultiTenancySides.Tenant,
            TenantId: Guid.NewGuid(),
            ClientId: null);

        await RoleCacheInvalidationHandler.HandleAsync(
            @event, cache, TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            PermissionChecker.RoleTag("Manager"),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoleUpdated_DescriptionOnly_NoCacheCall()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new RoleUpdatedEvent(
            RoleId: Guid.NewGuid(),
            Name: "Manager",
            PreviousName: null,
            MultiTenancySides: MultiTenancySides.Tenant,
            TenantId: Guid.NewGuid(),
            ClientId: null);

        await RoleCacheInvalidationHandler.HandleAsync(
            @event, cache, TestContext.Current.CancellationToken);

        await cache.DidNotReceive().RemoveByTagAsync(
            Arg.Any<string>(),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoleDeleted_RemovesByRoleNameTag()
    {
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new RoleDeletedEvent(
            RoleId: Guid.NewGuid(),
            Name: "Disposable",
            MultiTenancySides: MultiTenancySides.Both,
            TenantId: null,
            ClientId: null);

        await RoleCacheInvalidationHandler.HandleAsync(
            @event, cache, TestContext.Current.CancellationToken);

        await cache.Received(1).RemoveByTagAsync(
            PermissionChecker.RoleTag("Disposable"),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }
}
