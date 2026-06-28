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

public sealed class DefaultGeocodingServiceTests
{
    private static readonly PostalAddress Brussels =
        new(Street: "Rue de la Loi 16", PostalCode: "1000", Locality: "Brussels", Country: "BE");

    private static readonly GeoCoordinate BrusselsPoint = new(50.8503, 4.3517);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GeocodeAsync_NoProviders_ReturnsNullAndNeverThrows()
    {
        DefaultGeocodingService sut = CreateService([]);

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GeocodeAsync_NullAddress_Throws()
    {
        DefaultGeocodingService sut = CreateService([StubProvider("Nominatim", Brussels, BrusselsPoint)]);

        await Should.ThrowAsync<ArgumentNullException>(async () => await sut.GeocodeAsync(null!, Ct));
    }

    [Fact]
    public async Task GeocodeAsync_BlankLocalityAndCountry_SkipsWithoutCallingProvider()
    {
        IGeocodingProvider provider = StubProvider("Nominatim", Brussels, BrusselsPoint);
        DefaultGeocodingService sut = CreateService([provider]);

        PostalAddress empty = new(Street: null, PostalCode: null, Locality: "  ", Country: "");
        (await sut.GeocodeAsync(empty, Ct)).ShouldBeNull();
        await provider.DidNotReceive().ResolveAsync(Arg.Any<PostalAddress>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeocodeAsync_FirstProviderHits_ReturnsItsResult()
    {
        DefaultGeocodingService sut = CreateService([StubProvider("Nominatim", Brussels, BrusselsPoint)]);

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBe(BrusselsPoint);
    }

    [Fact]
    public async Task GeocodeAsync_FirstProviderMisses_FallsBackToNext()
    {
        IGeocodingProvider first = StubProvider("First", Brussels, result: null);
        IGeocodingProvider second = StubProvider("Second", Brussels, BrusselsPoint);

        DefaultGeocodingService sut = CreateService(
            [first, second],
            new GranitGeocodingOptions { ProviderOrder = { "First", "Second" } });

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBe(BrusselsPoint);
    }

    [Fact]
    public async Task GeocodeAsync_FirstProviderThrows_DegradesToNextWithoutThrowing()
    {
        IGeocodingProvider faulty = Substitute.For<IGeocodingProvider>();
        faulty.ProviderName.Returns("Faulty");
        faulty.ResolveAsync(Brussels, Arg.Any<CancellationToken>())
            .Returns<Task<GeoCoordinate?>>(_ => throw new InvalidOperationException("boom"));
        IGeocodingProvider healthy = StubProvider("Healthy", Brussels, BrusselsPoint);

        DefaultGeocodingService sut = CreateService(
            [faulty, healthy],
            new GranitGeocodingOptions { ProviderOrder = { "Faulty", "Healthy" } });

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBe(BrusselsPoint);
    }

    [Fact]
    public async Task GeocodeAsync_RepeatedLookup_IsCachedAndProviderCalledOnce()
    {
        IGeocodingProvider provider = StubProvider("Nominatim", Brussels, BrusselsPoint);
        DefaultGeocodingService sut = CreateService([provider]);

        await sut.GeocodeAsync(Brussels, Ct);
        await sut.GeocodeAsync(Brussels, Ct);

        await provider.Received(1).ResolveAsync(Brussels, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeocodeAsync_NegativeResult_IsCached()
    {
        IGeocodingProvider provider = StubProvider("Nominatim", Brussels, result: null);
        DefaultGeocodingService sut = CreateService([provider]);

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBeNull();
        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBeNull();

        await provider.Received(1).ResolveAsync(Brussels, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeocodeAsync_NormalisedEquivalentAddress_SharesCacheEntry()
    {
        // Same address, different casing/padding — must hit one provider call thanks to key normalisation.
        IGeocodingProvider provider = Substitute.For<IGeocodingProvider>();
        provider.ProviderName.Returns("Nominatim");
        provider.ResolveAsync(Arg.Any<PostalAddress>(), Arg.Any<CancellationToken>()).Returns(BrusselsPoint);
        DefaultGeocodingService sut = CreateService([provider]);

        await sut.GeocodeAsync(new PostalAddress("Rue de la Loi 16", "1000", "Brussels", "BE"), Ct);
        await sut.GeocodeAsync(new PostalAddress("  rue de la   LOI 16 ", "1000", " brussels ", "be"), Ct);

        await provider.Received(1).ResolveAsync(Arg.Any<PostalAddress>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeocodeAsync_ProviderOrder_DeterminesPriority()
    {
        IGeocodingProvider first = StubProvider("First", Brussels, new GeoCoordinate(1, 1));
        IGeocodingProvider second = StubProvider("Second", Brussels, new GeoCoordinate(2, 2));

        DefaultGeocodingService sut = CreateService(
            [first, second],
            new GranitGeocodingOptions { ProviderOrder = { "Second", "First" } });

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBe(new GeoCoordinate(2, 2));
        await first.DidNotReceive().ResolveAsync(Arg.Any<PostalAddress>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GeocodeAsync_UnknownProviderInOrder_IsSkipped()
    {
        IGeocodingProvider provider = StubProvider("Nominatim", Brussels, BrusselsPoint);
        DefaultGeocodingService sut = CreateService(
            [provider],
            new GranitGeocodingOptions { ProviderOrder = { "DoesNotExist" } });

        (await sut.GeocodeAsync(Brussels, Ct)).ShouldBeNull();
    }

    [Fact]
    public void NormalizeKey_TrimsLowercasesAndCollapsesWhitespace()
    {
        string key = DefaultGeocodingService.NormalizeKey(
            new PostalAddress("  Rue   de la  Loi 16 ", "1000", " Brussels ", "BE"));

        key.ShouldBe("rue de la loi 16|1000|brussels|be");
    }

    [Fact]
    public void NormalizeKey_NullOptionalComponents_BecomeEmptySegments()
    {
        string key = DefaultGeocodingService.NormalizeKey(
            new PostalAddress(Street: null, PostalCode: null, Locality: "Brussels", Country: "BE"));

        key.ShouldBe("||brussels|be");
    }

    [Fact]
    public void BuildCacheKey_HashesKey_NeverEmbedsRawAddress()
    {
        string normalized = "rue de la loi 16|1000|brussels|be";

        string key = DefaultGeocodingService.BuildCacheKey(normalized);

        key.ShouldStartWith("granit:geocoding:");
        key.ShouldNotContain("brussels");
        key.ShouldBe(DefaultGeocodingService.BuildCacheKey(normalized));                  // deterministic
        key.ShouldNotBe(DefaultGeocodingService.BuildCacheKey("paris||paris|fr"));        // distinct per address
    }

    private static IGeocodingProvider StubProvider(string name, PostalAddress address, GeoCoordinate? result)
    {
        IGeocodingProvider provider = Substitute.For<IGeocodingProvider>();
        provider.ProviderName.Returns(name);
        provider.ResolveAsync(address, Arg.Any<CancellationToken>()).Returns(result);
        return provider;
    }

    private static DefaultGeocodingService CreateService(
        IEnumerable<IGeocodingProvider> providers,
        GranitGeocodingOptions? options = null) =>
        new(
            providers,
            new FusionCache(new FusionCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitGeocodingOptions()),
            CreateMetrics(),
            NullLogger<DefaultGeocodingService>.Instance);

    private static GeocodingMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(call => new Meter(call.Arg<MeterOptions>().Name));
        return new GeocodingMetrics(factory);
    }
}
