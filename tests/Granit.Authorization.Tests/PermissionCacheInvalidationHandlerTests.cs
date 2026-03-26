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
}
