using System.Diagnostics.Metrics;
using Granit.IpGeolocation.Diagnostics;
using Granit.IpGeolocation.Internal;
using Granit.IpGeolocation.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.IpGeolocation.Tests;

public sealed class DefaultIpGeolocationResolverTests
{
    private const string PublicIp = "8.8.8.8";

    private static readonly GeoLocation Brussels =
        new() { City = "Brussels", Country = "Belgium", CountryCode = "BE" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ResolveAsync_NoProviders_ReturnsNullAndNeverThrows()
    {
        DefaultIpGeolocationResolver sut = CreateResolver([]);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-ip")]
    public async Task ResolveAsync_AbsentOrUnparseableIp_ReturnsNull(string? ip)
    {
        IIpGeolocationProvider provider = StubProvider("MaxMind", PublicIp, Brussels);
        DefaultIpGeolocationResolver sut = CreateResolver([provider]);

        (await sut.ResolveAsync(ip, Ct)).ShouldBeNull();
        await provider.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_PrivateIp_ShortCircuitsWithoutCallingProvider()
    {
        IIpGeolocationProvider provider = StubProvider("MaxMind", "10.0.0.1", Brussels);
        DefaultIpGeolocationResolver sut = CreateResolver([provider]);

        (await sut.ResolveAsync("10.0.0.1", Ct)).ShouldBeNull();
        await provider.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_FirstProviderHits_ReturnsItsResult()
    {
        DefaultIpGeolocationResolver sut = CreateResolver([StubProvider("MaxMind", PublicIp, Brussels)]);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBe(Brussels);
    }

    [Fact]
    public async Task ResolveAsync_FirstProviderMisses_FallsBackToNext()
    {
        IIpGeolocationProvider first = StubProvider("MaxMind", PublicIp, result: null);
        IIpGeolocationProvider second = StubProvider("IpApi", PublicIp, Brussels);

        DefaultIpGeolocationResolver sut = CreateResolver(
            [first, second],
            new GranitIpGeolocationOptions { ProviderOrder = { "MaxMind", "IpApi" } });

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBe(Brussels);
    }

    [Fact]
    public async Task ResolveAsync_FirstProviderThrows_DegradesToNextWithoutThrowing()
    {
        IIpGeolocationProvider faulty = Substitute.For<IIpGeolocationProvider>();
        faulty.ProviderName.Returns("MaxMind");
        faulty.ResolveAsync(PublicIp, Arg.Any<CancellationToken>())
            .Returns<Task<GeoLocation?>>(_ => throw new InvalidOperationException("boom"));
        IIpGeolocationProvider healthy = StubProvider("IpApi", PublicIp, Brussels);

        DefaultIpGeolocationResolver sut = CreateResolver(
            [faulty, healthy],
            new GranitIpGeolocationOptions { ProviderOrder = { "MaxMind", "IpApi" } });

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBe(Brussels);
    }

    [Fact]
    public async Task ResolveAsync_RepeatedLookup_IsCachedAndProviderCalledOnce()
    {
        IIpGeolocationProvider provider = StubProvider("MaxMind", PublicIp, Brussels);
        DefaultIpGeolocationResolver sut = CreateResolver([provider]);

        await sut.ResolveAsync(PublicIp, Ct);
        await sut.ResolveAsync(PublicIp, Ct);

        await provider.Received(1).ResolveAsync(PublicIp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_NegativeResult_IsCached()
    {
        IIpGeolocationProvider provider = StubProvider("MaxMind", PublicIp, result: null);
        DefaultIpGeolocationResolver sut = CreateResolver([provider]);

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();

        await provider.Received(1).ResolveAsync(PublicIp, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ProviderOrder_DeterminesPriority()
    {
        IIpGeolocationProvider maxMind = StubProvider("MaxMind", PublicIp, Brussels);
        GeoLocation paris = new() { City = "Paris", CountryCode = "FR" };
        IIpGeolocationProvider ipApi = StubProvider("IpApi", PublicIp, paris);

        DefaultIpGeolocationResolver sut = CreateResolver(
            [maxMind, ipApi],
            new GranitIpGeolocationOptions { ProviderOrder = { "IpApi", "MaxMind" } });

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBe(paris);
        await maxMind.DidNotReceive().ResolveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_UnknownProviderInOrder_IsSkipped()
    {
        IIpGeolocationProvider provider = StubProvider("MaxMind", PublicIp, Brussels);
        DefaultIpGeolocationResolver sut = CreateResolver(
            [provider],
            new GranitIpGeolocationOptions { ProviderOrder = { "DoesNotExist" } });

        (await sut.ResolveAsync(PublicIp, Ct)).ShouldBeNull();
    }

    [Fact]
    public void BuildCacheKey_HashesIp_NeverEmbedsRawAddress()
    {
        const string ip = "203.0.113.42";

        string key = DefaultIpGeolocationResolver.BuildCacheKey(ip, secret: null);

        key.ShouldStartWith("granit:ip_geolocation:");
        key.ShouldNotContain(ip);
        key.ShouldBe(DefaultIpGeolocationResolver.BuildCacheKey(ip, secret: null));            // deterministic
        key.ShouldNotBe(DefaultIpGeolocationResolver.BuildCacheKey("8.8.8.8", secret: null));  // distinct per address
    }

    [Fact]
    public void BuildCacheKey_WithSecret_ProducesKeyedHashDistinctFromUnkeyed()
    {
        const string ip = "203.0.113.42";

        string keyed = DefaultIpGeolocationResolver.BuildCacheKey(ip, secret: "pepper");

        keyed.ShouldStartWith("granit:ip_geolocation:");
        keyed.ShouldNotContain(ip);
        keyed.ShouldNotBe(DefaultIpGeolocationResolver.BuildCacheKey(ip, secret: null));      // not a plain SHA-256
        keyed.ShouldNotBe(DefaultIpGeolocationResolver.BuildCacheKey(ip, secret: "other"));   // key-dependent
        keyed.ShouldBe(DefaultIpGeolocationResolver.BuildCacheKey(ip, secret: "pepper"));     // deterministic
    }

    private static IIpGeolocationProvider StubProvider(string name, string ip, GeoLocation? result)
    {
        IIpGeolocationProvider provider = Substitute.For<IIpGeolocationProvider>();
        provider.ProviderName.Returns(name);
        provider.ResolveAsync(ip, Arg.Any<CancellationToken>()).Returns(result);
        return provider;
    }

    private static DefaultIpGeolocationResolver CreateResolver(
        IEnumerable<IIpGeolocationProvider> providers,
        GranitIpGeolocationOptions? options = null) =>
        new(
            providers,
            new FusionCache(new FusionCacheOptions()),
            Microsoft.Extensions.Options.Options.Create(options ?? new GranitIpGeolocationOptions()),
            CreateMetrics(),
            NullLogger<DefaultIpGeolocationResolver>.Instance);

    private static IpGeolocationMetrics CreateMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(call => new Meter(call.Arg<MeterOptions>().Name));
        return new IpGeolocationMetrics(factory);
    }
}
