// =============================================================================
// GlobalSettingValueProviderTests - Unit tests for the Global provider
// =============================================================================
// Verifies cache pass-through for reads, cache invalidation on Set/Clear,
// and the sentinel pattern (Value=null -> provider returns null).
// =============================================================================

using Granit.Settings.Definitions;
using Granit.Settings.Options;
using Granit.Settings.Providers;
using Granit.Settings.Stores;
using Granit.Settings.Values;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Settings.Tests;

public sealed class GlobalSettingValueProviderTests
{
    private static (GlobalSettingValueProvider provider, InMemorySettingStore store, IFusionCache cache)
        Create()
    {
        InMemorySettingStore store = new();
        // Use a real in-memory FusionCache for read path (naturally calls factory on miss)
        IFusionCache cache = new FusionCache(new FusionCacheOptions());
        IOptions<SettingsOptions> options = Microsoft.Extensions.Options.Options.Create(new SettingsOptions());
        GlobalSettingValueProvider provider = new(store, store, cache, options);
        return (provider, store, cache);
    }

    private static (GlobalSettingValueProvider provider, InMemorySettingStore store, IFusionCache cache)
        CreateWithMockCache()
    {
        InMemorySettingStore store = new();
        IFusionCache cache = Substitute.For<IFusionCache>();
        IOptions<SettingsOptions> options = Microsoft.Extensions.Options.Options.Create(new SettingsOptions());
        GlobalSettingValueProvider provider = new(store, store, cache, options);
        return (provider, store, cache);
    }

    [Fact]
    public void Name_Is_G() =>
        Create().provider.Name.ShouldBe("G");

    [Fact]
    public void Order_Is_300() =>
        Create().provider.Order.ShouldBe(300);

    [Fact]
    public async Task GetOrNullAsync_StoreHasValue_Returns_SettingValue()
    {
        (GlobalSettingValueProvider provider, InMemorySettingStore store, _) = Create();
        SettingDefinition def = new("App.Theme");
        await store.SetAsync("App.Theme", "G", null, "dark", TestContext.Current.CancellationToken);

        SettingValue? result = await provider.GetOrNullAsync(def, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.Value.ShouldBe("dark");
        result.ProviderName.ShouldBe("G");
        result.ProviderKey.ShouldBeNull();
    }

    [Fact]
    public async Task GetOrNullAsync_StoreEmpty_Returns_Null()
    {
        (GlobalSettingValueProvider provider, _, _) = Create();
        SettingDefinition def = new("App.Theme");

        SettingValue? result = await provider.GetOrNullAsync(def, TestContext.Current.CancellationToken);

        result.ShouldBeNull("sentinel with Value=null must be filtered out");
    }

    [Fact]
    public async Task SetAsync_WritesToStore_And_InvalidatesCache()
    {
        (GlobalSettingValueProvider provider, InMemorySettingStore store, IFusionCache cache) = CreateWithMockCache();
        SettingDefinition def = new("App.Theme");

        await provider.SetAsync(def, "light", TestContext.Current.CancellationToken);

        SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);
        stored!.Value.ShouldBe("light");

        await cache.Received(1).ExpireAsync(
            Arg.Is<string>(k => k.Contains('G') && k.Contains("App.Theme")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearAsync_DeletesFromStore_And_InvalidatesCache()
    {
        (GlobalSettingValueProvider provider, InMemorySettingStore store, IFusionCache cache) = CreateWithMockCache();
        SettingDefinition def = new("App.Theme");
        await store.SetAsync("App.Theme", "G", null, "dark", TestContext.Current.CancellationToken);

        await provider.ClearAsync(def, TestContext.Current.CancellationToken);

        SettingValue? stored = await store.GetOrNullAsync(
            "App.Theme", "G", null, TestContext.Current.CancellationToken);
        stored.ShouldBeNull("ClearAsync must delete the store entry");

        await cache.Received(1).ExpireAsync(
            Arg.Is<string>(k => k.Contains('G') && k.Contains("App.Theme")),
            Arg.Any<FusionCacheEntryOptions?>(),
            Arg.Any<CancellationToken>());
    }
}
