using Granit.Localization.Internal;
using Granit.Localization.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Localization.Tests;

public sealed class CachedLocalizationOverrideStoreTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static CachedLocalizationOverrideStore BuildStore(
        ILocalizationOverrideStoreReader innerReader,
        ILocalizationOverrideStoreWriter innerWriter,
        TimeSpan? cacheTtl = null)
    {
        ServiceCollection services = new();
        services.AddKeyedScoped<ILocalizationOverrideStoreReader>(
            CachedLocalizationOverrideStore.RawStoreKey, (_, _) => innerReader);
        services.AddKeyedScoped<ILocalizationOverrideStoreWriter>(
            CachedLocalizationOverrideStore.RawStoreKey, (_, _) => innerWriter);

        IFusionCache fusionCache = new FusionCache(new FusionCacheOptions());
        IOptions<LocalizationOverridesCacheOptions> options = Microsoft.Extensions.Options.Options.Create(
            new LocalizationOverridesCacheOptions { CacheTtl = cacheTtl ?? TimeSpan.FromMinutes(5) });

        ServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new CachedLocalizationOverrideStore(fusionCache, options, scopeFactory, sp);
    }

    // -------------------------------------------------------------------------
    // GetOverridesAsync — cache miss / hit
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOverridesAsync_CacheMiss_CallsInnerStore()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
        innerReader.GetOverridesAsync("TestApp", "fr", Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(Task.FromResult<IReadOnlyDictionary<string, string>>(
                 new Dictionary<string, string> { ["Key1"] = "Valeur1" }));

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);
        IReadOnlyDictionary<string, string> result =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        result["Key1"].ShouldBe("Valeur1");
        await innerReader.Received(1).GetOverridesAsync("TestApp", "fr", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetOverridesAsync_CacheHit_DoesNotCallInnerStoreAgain()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
        innerReader.GetOverridesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(Task.FromResult<IReadOnlyDictionary<string, string>>(
                 new Dictionary<string, string> { ["Key1"] = "Valeur1" }));

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);

        await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);
        await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        await innerReader.Received(1).GetOverridesAsync("TestApp", "fr", Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // SetOverrideAsync — forwards + invalidates cache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetOverrideAsync_ForwardsToInnerStore()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
        innerWriter.SetOverrideAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(Task.CompletedTask);

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);
        await store.SetOverrideAsync(
            "TestApp", "fr", "Patient.Title", "Bénéficiaire",
            TestContext.Current.CancellationToken);

        await innerWriter.Received(1).SetOverrideAsync(
            "TestApp", "fr", "Patient.Title", "Bénéficiaire", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetOverrideAsync_InvalidatesCache()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();

        int callCount = 0;
        innerReader.GetOverridesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(_ =>
        {
            callCount++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string> { ["Key"] = $"value{callCount}" });
        });
        innerWriter.SetOverrideAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(Task.CompletedTask);

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);

        // First call populates cache
        IReadOnlyDictionary<string, string> first =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        // Write invalidates cache
        await store.SetOverrideAsync("TestApp", "fr", "Key", "updated",
            TestContext.Current.CancellationToken);

        // Second call should hit inner store again (cache was invalidated)
        IReadOnlyDictionary<string, string> second =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        first["Key"].ShouldBe("value1");
        second["Key"].ShouldBe("value2");
        await innerReader.Received(2).GetOverridesAsync("TestApp", "fr", Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // RemoveOverrideAsync — forwards + invalidates cache
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RemoveOverrideAsync_ForwardsToInnerStore()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
        innerWriter.RemoveOverrideAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(Task.CompletedTask);

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);
        await store.RemoveOverrideAsync(
            "TestApp", "fr", "Patient.Title",
            TestContext.Current.CancellationToken);

        await innerWriter.Received(1).RemoveOverrideAsync(
            "TestApp", "fr", "Patient.Title", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveOverrideAsync_InvalidatesCache()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();

        int callCount = 0;
        innerReader.GetOverridesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(_ =>
        {
            callCount++;
            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>());
        });
        innerWriter.RemoveOverrideAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
             .ReturnsForAnyArgs(Task.CompletedTask);

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);

        await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);
        await store.RemoveOverrideAsync("TestApp", "fr", "Key",
            TestContext.Current.CancellationToken);
        await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);

        await innerReader.Received(2).GetOverridesAsync("TestApp", "fr", Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Cache key uniqueness (culture isolation)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetOverridesAsync_DifferentCultures_AreIsolatedInCache()
    {
        ILocalizationOverrideStoreReader innerReader = Substitute.For<ILocalizationOverrideStoreReader>();
        ILocalizationOverrideStoreWriter innerWriter = Substitute.For<ILocalizationOverrideStoreWriter>();
        innerReader.GetOverridesAsync("TestApp", "fr", Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(
                 new Dictionary<string, string> { ["Key"] = "Français" }));
        innerReader.GetOverridesAsync("TestApp", "en", Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<IReadOnlyDictionary<string, string>>(
                 new Dictionary<string, string> { ["Key"] = "English" }));

        CachedLocalizationOverrideStore store = BuildStore(innerReader, innerWriter);

        IReadOnlyDictionary<string, string> fr =
            await store.GetOverridesAsync("TestApp", "fr", TestContext.Current.CancellationToken);
        IReadOnlyDictionary<string, string> en =
            await store.GetOverridesAsync("TestApp", "en", TestContext.Current.CancellationToken);

        fr["Key"].ShouldBe("Français");
        en["Key"].ShouldBe("English");
    }
}
