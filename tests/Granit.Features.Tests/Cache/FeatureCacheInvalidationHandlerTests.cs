using Granit.Features.Cache;
using Granit.Features.Events;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Features.Tests.Cache;

public sealed class FeatureCacheInvalidationHandlerTests
{
    [Fact]
    public async Task HandleAsync_TenantEvent_ExpiresCacheEntryWithCorrectKey()
    {
        var tenantId = Guid.NewGuid();
        FeatureValueChangedEvent @event = new("App.VideoConsultation", tenantId);
        IFusionCache cache = Substitute.For<IFusionCache>();

        await FeatureCacheInvalidationHandler.HandleAsync(
            @event, cache, TestContext.Current.CancellationToken);

        string expectedKey = FeatureCacheKey.Build(tenantId, "App.VideoConsultation");
        await cache.Received(1).ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_GlobalEvent_ExpiresCacheEntryWithGlobalKey()
    {
        FeatureValueChangedEvent @event = new("App.Feature", TenantId: null);
        IFusionCache cache = Substitute.For<IFusionCache>();

        await FeatureCacheInvalidationHandler.HandleAsync(
            @event, cache, TestContext.Current.CancellationToken);

        string expectedKey = FeatureCacheKey.Build(null, "App.Feature");
        await cache.Received(1).ExpireAsync(expectedKey, Arg.Any<FusionCacheEntryOptions?>(), Arg.Any<CancellationToken>());
    }
}
