using Granit.IpGeolocation.MaxMind.Internal;
using Granit.IpGeolocation.MaxMind.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.MaxMind.Tests;

public sealed class MaxMindIpGeolocationProviderTests
{
    private static readonly string CityDb = Path.Combine(AppContext.BaseDirectory, "TestData", "GeoIP2-City-Test.mmdb");

    private static MaxMindIpGeolocationProvider Build(string providerName = "MaxMind")
    {
        MaxMindIpGeolocationOptions options = new()
        {
            DatabasePath = CityDb,
            ReloadOnChange = false,
            ProviderName = providerName,
        };
        IOptions<MaxMindIpGeolocationOptions> accessor = Microsoft.Extensions.Options.Options.Create(options);
        var database = new MaxMindDatabaseProvider(accessor, NullLogger<MaxMindDatabaseProvider>.Instance);
        return new MaxMindIpGeolocationProvider(database, accessor);
    }

    [Fact]
    public void ProviderName_comes_from_options() =>
        Build(providerName: "OnPremGeo").ProviderName.ShouldBe("OnPremGeo");

    [Fact]
    public async Task ResolveAsync_returns_a_location_for_a_known_address()
    {
        MaxMindIpGeolocationProvider provider = Build();

        GeoLocation? location = await provider.ResolveAsync("2.125.160.216", TestContext.Current.CancellationToken);

        location.ShouldNotBeNull();
        location.CountryCode.ShouldBe("GB");
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_an_unparseable_address()
    {
        MaxMindIpGeolocationProvider provider = Build();

        GeoLocation? location = await provider.ResolveAsync("not-an-ip", TestContext.Current.CancellationToken);

        location.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_an_unknown_address()
    {
        MaxMindIpGeolocationProvider provider = Build();

        GeoLocation? location = await provider.ResolveAsync("10.0.0.0", TestContext.Current.CancellationToken);

        location.ShouldBeNull();
    }
}
