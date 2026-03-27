using Granit.Authorization.Cache;
using Granit.Authorization.Events;
using Granit.Authorization.Services;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Authorization.Tests;

public sealed class PermissionCacheInvalidationHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_ExpiresCacheEntryWithCorrectKey()
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();

        var @event = new PermissionGrantChangedEvent("Invoices.Delete", "accountant", TenantId, IsGranted: true);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        // Assert
        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, "accountant", "Invoices.Delete");
        await cache.Received(1).ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GlobalScope_UsesGlobalCacheKey()
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();

        var @event = new PermissionGrantChangedEvent("Invoices.Delete", "accountant", TenantId: null, IsGranted: false);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        // Assert
        string expectedKey = PermissionChecker.BuildCacheKey(null, "accountant", "Invoices.Delete");
        expectedKey.ShouldStartWith("perm:global:");
        await cache.Received(1).RemoveAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // VULN-204: Grant vs Revoke invalidation strategy
    // =========================================================================

    [Fact]
    public async Task HandleAsync_GrantCreated_CallsExpireAsync()
    {
        // Arrange — VULN-204: grants use soft-expire (stale "denied" is safe)
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new PermissionGrantChangedEvent("Orders.Create", "editor", TenantId, IsGranted: true);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        // Assert — ExpireAsync called, NOT RemoveAsync
        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, "editor", "Orders.Create");
        await cache.Received(1).ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().RemoveAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GrantRevoked_CallsRemoveAsync()
    {
        // Arrange — VULN-204: revocations use hard-remove to prevent stale-while-revalidate
        IFusionCache cache = Substitute.For<IFusionCache>();
        var @event = new PermissionGrantChangedEvent("Orders.Create", "editor", TenantId, IsGranted: false);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(@event, cache, TestContext.Current.CancellationToken);

        // Assert — RemoveAsync called, NOT ExpireAsync
        string expectedKey = PermissionChecker.BuildCacheKey(TenantId, "editor", "Orders.Create");
        await cache.Received(1).RemoveAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.DidNotReceive().ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    // =========================================================================
    // Cache key correctness
    // =========================================================================

    [Fact]
    public async Task HandleAsync_DifferentPermissions_UseDifferentCacheKeys()
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();
        var event1 = new PermissionGrantChangedEvent("Invoices.Read", "editor", TenantId, IsGranted: true);
        var event2 = new PermissionGrantChangedEvent("Invoices.Delete", "editor", TenantId, IsGranted: true);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(event1, cache, TestContext.Current.CancellationToken);
        await PermissionCacheInvalidationHandler.HandleAsync(event2, cache, TestContext.Current.CancellationToken);

        // Assert
        string key1 = PermissionChecker.BuildCacheKey(TenantId, "editor", "Invoices.Read");
        string key2 = PermissionChecker.BuildCacheKey(TenantId, "editor", "Invoices.Delete");
        key1.ShouldNotBe(key2);

        await cache.Received(1).ExpireAsync(key1, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.Received(1).ExpireAsync(key2, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DifferentRoles_UseDifferentCacheKeys()
    {
        // Arrange
        IFusionCache cache = Substitute.For<IFusionCache>();
        var event1 = new PermissionGrantChangedEvent("Invoices.Delete", "accountant", TenantId, IsGranted: false);
        var event2 = new PermissionGrantChangedEvent("Invoices.Delete", "editor", TenantId, IsGranted: false);

        // Act
        await PermissionCacheInvalidationHandler.HandleAsync(event1, cache, TestContext.Current.CancellationToken);
        await PermissionCacheInvalidationHandler.HandleAsync(event2, cache, TestContext.Current.CancellationToken);

        // Assert
        string key1 = PermissionChecker.BuildCacheKey(TenantId, "accountant", "Invoices.Delete");
        string key2 = PermissionChecker.BuildCacheKey(TenantId, "editor", "Invoices.Delete");
        key1.ShouldNotBe(key2);

        await cache.Received(1).RemoveAsync(key1, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync(key2, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }
}
