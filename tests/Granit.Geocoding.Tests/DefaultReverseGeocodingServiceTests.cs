using System.Diagnostics.Metrics;
using Granit.Domain.ValueObjects;
using Granit.Geocoding.Diagnostics;
using Granit.Geocoding.Internal;
using Granit.Geocoding.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Geocoding.Tests;

public sealed class DefaultReverseGeocodingServiceTests
{
    private static readonly GeoCoordinate Brussels = new(50.8503, 4.3517);

    private static readonly ReverseGeocodingResult BrusselsResult =
        new(new PostalAddress("Rue de la Loi 16", "1000", "Brussels", "BE"), GeocodeMatchPrecision.Rooftop);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReverseAsync_NoProviders_ReturnsNullAndNeverThrows()
    {
        DefaultReverseGeocodingService sut = CreateService([]);

        (await sut.ReverseAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ReverseAsync_NullCoordinate_Throws()
    {
        DefaultReverseGeocodingService sut = CreateService([StubProvider("Nominatim", BrusselsResult)]);

        await Should.ThrowAsync<ArgumentNullException>(async () => await sut.ReverseAsync(null!, Ct));
    }

    [Fact]
    public async Task ReverseAsync_FirstProviderHits_ReturnsItsResult()
    {
        DefaultReverseGeocodingService sut = CreateService([StubProvider("Nominatim", BrusselsResult)]);

        (await sut.ReverseAsync(Brussels, Ct)).ShouldBe(BrusselsResult);
    }

    [Fact]
    public async Task ReverseAsync_FirstProviderMisses_FallsBackToNext()
    {
        IReverseGeocodingProvider first = StubProvider("First", result: null);
        IReverseGeocodingProvider second = StubProvider("Second", BrusselsResult);

        DefaultReverseGeocodingService sut = CreateService(
            [first, second],
            new GranitGeocodingOptions { ProviderOrder = { "First", "Second" } });

        (await sut.ReverseAsync(Brussels, Ct)).ShouldBe(BrusselsResult);
    }

    [Fact]
    public async Task ReverseAsync_FirstProviderThrows_DegradesToNextWithoutThrowing()
    {
        IReverseGeocodingProvider faulty = Substitute.For<IReverseGeocodingProvider>();
        faulty.ProviderName.Returns("Faulty");
        faulty.ReverseAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>())
            .Returns<Task<ReverseGeocodingResult?>>(_ => throw new InvalidOperationException("boom"));
        IReverseGeocodingProvider healthy = StubProvider("Healthy", BrusselsResult);

        DefaultReverseGeocodingService sut = CreateService(
            [faulty, healthy],
            new GranitGeocodingOptions { ProviderOrder = { "Faulty", "Healthy" } });

        (await sut.ReverseAsync(Brussels, Ct)).ShouldBe(BrusselsResult);
    }

    [Fact]
    public async Task ReverseAsync_RepeatedLookup_IsCachedAndProviderCalledOnce()
    {
        IReverseGeocodingProvider provider = StubProvider("Nominatim", BrusselsResult);
        DefaultReverseGeocodingService sut = CreateService([provider]);

        await sut.ReverseAsync(Brussels, Ct);
        await sut.ReverseAsync(Brussels, Ct);

        await provider.Received(1).ReverseAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReverseAsync_ProviderOrder_DeterminesPriority()
    {
        ReverseGeocodingResult paris =
            new(new PostalAddress(null, null, "Paris", "FR"), GeocodeMatchPrecision.Locality);
        IReverseGeocodingProvider first = StubProvider("First", BrusselsResult);
        IReverseGeocodingProvider second = StubProvider("Second", paris);

        DefaultReverseGeocodingService sut = CreateService(
            [first, second],
            new GranitGeocodingOptions { ProviderOrder = { "Second", "First" } });

        (await sut.ReverseAsync(Brussels, Ct)).ShouldBe(paris);
        await first.DidNotReceive().ReverseAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildCacheKey_HashesCoordinate_NeverEmbedsRaw()
    {
        string key = DefaultReverseGeocodingService.BuildCacheKey(new GeoCoordinate(50.8503, 4.3517));

        key.ShouldStartWith("granit:geocoding:reverse:");
        key.ShouldNotContain("50.85");
        key.ShouldBe(DefaultReverseGeocodingService.BuildCacheKey(new GeoCoordinate(50.8503, 4.3517)));  // deterministic
        key.ShouldNotBe(DefaultReverseGeocodingService.BuildCacheKey(new GeoCoordinate(48.8566, 2.3522)));
    }

    private static IReverseGeocodingProvider StubProvider(string name, ReverseGeocodingResult? result)
    {
        IReverseGeocodingProvider provider = Substitute.For<IReverseGeocodingProvider>();
        provider.ProviderName.Returns(name);
        provider.ReverseAsync(Arg.Any<GeoCoordinate>(), Arg.Any<CancellationToken>()).Returns(result);
        return provider;
    }

    private static DefaultReverseGeocodingService CreateService(
        IEnumerable<IReverseGeocodingProvider> providers,
        GranitGeocodingOptions? options = null) =>
        new(
            providers,
            new FusionCache(new FusionCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitGeocodingOptions()),
            CreateMetrics(),
            NullLogger<DefaultReverseGeocodingService>.Instance);

    private static GeocodingMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(call => new Meter(call.Arg<MeterOptions>().Name));
        return new GeocodingMetrics(factory);
    }
}
