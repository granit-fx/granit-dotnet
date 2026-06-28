using System.Net;
using Granit.IpGeolocation.MaxMind.Internal;
using Granit.IpGeolocation.MaxMind.Options;
using MaxMind.Db;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.MaxMind.Tests;

public sealed class MaxMindDatabaseProviderTests
{
    private static readonly string CityDb = Path.Combine(AppContext.BaseDirectory, "TestData", "GeoIP2-City-Test.mmdb");
    private static readonly string CountryDb = Path.Combine(AppContext.BaseDirectory, "TestData", "GeoLite2-Country-Test.mmdb");

    // Well-known IPs present in MaxMind's sample databases.
    private const string LondonCityIp = "2.125.160.216"; // Boxford / West Berkshire, GB
    private const string LondonCountryIp = "81.2.69.160"; // London, GB
    private const string AbsentIp = "10.0.0.0";           // private range, not in the test data

    private static MaxMindDatabaseProvider Open(string databasePath, bool reloadOnChange = false) =>
        new(Microsoft.Extensions.Options.Options.Create(
                new MaxMindIpGeolocationOptions { DatabasePath = databasePath, ReloadOnChange = reloadOnChange }),
            NullLogger<MaxMindDatabaseProvider>.Instance);

    [Fact]
    public async Task Constructor_NonMmdbFile_ThrowsInvalidDatabaseException()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "this is not a maxmind database", TestContext.Current.CancellationToken);

        try
        {
            MaxMindIpGeolocationOptions options = new() { DatabasePath = path, ReloadOnChange = false };

            Should.Throw<InvalidDatabaseException>(() =>
                new MaxMindDatabaseProvider(
                    Microsoft.Extensions.Options.Options.Create(options),
                    NullLogger<MaxMindDatabaseProvider>.Instance));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Lookup_against_a_city_database_returns_city_level_detail()
    {
        using MaxMindDatabaseProvider provider = Open(CityDb);

        GeoLocation? location = provider.Lookup(IPAddress.Parse(LondonCityIp));

        location.ShouldNotBeNull();
        location.CountryCode.ShouldBe("GB");
        location.City.ShouldNotBeNullOrEmpty();
        location.Coordinate.ShouldNotBeNull();
        location.AccuracyRadiusKm.ShouldNotBeNull();
    }

    [Fact]
    public void Lookup_against_a_country_database_returns_country_only()
    {
        using MaxMindDatabaseProvider provider = Open(CountryDb);

        GeoLocation? location = provider.Lookup(IPAddress.Parse(LondonCountryIp));

        location.ShouldNotBeNull();
        location.CountryCode.ShouldBe("GB");
        // The country database carries no city / coordinate data.
        location.City.ShouldBeNull();
        location.Coordinate.ShouldBeNull();
    }

    [Fact]
    public void Lookup_returns_null_for_an_address_absent_from_the_database()
    {
        using MaxMindDatabaseProvider provider = Open(CityDb);

        provider.Lookup(IPAddress.Parse(AbsentIp)).ShouldBeNull();
    }

    [Fact]
    public void Constructor_with_reload_enabled_still_serves_lookups()
    {
        // Exercises the file-watcher wiring path; lookups must keep working.
        using MaxMindDatabaseProvider provider = Open(CityDb, reloadOnChange: true);

        provider.Lookup(IPAddress.Parse(LondonCityIp)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Hot_reload_swaps_in_the_new_database_on_file_change()
    {
        // Start from a country DB copy, then overwrite it with a city DB and assert the
        // watcher swaps the reader in — city-level fields appear without a restart.
        string temp = Path.Combine(Path.GetTempPath(), $"maxmind-reload-{Guid.NewGuid():N}.mmdb");
        File.Copy(CountryDb, temp);
        try
        {
            using MaxMindDatabaseProvider provider = Open(temp, reloadOnChange: true);
            provider.Lookup(IPAddress.Parse(LondonCountryIp))!.City.ShouldBeNull();

            File.Copy(CityDb, temp, overwrite: true);

            GeoLocation? reloaded = await Poll(
                () => provider.Lookup(IPAddress.Parse(LondonCityIp)),
                until: loc => loc?.City is not null,
                TestContext.Current.CancellationToken);

            reloaded.ShouldNotBeNull();
            reloaded.City.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private static async Task<T?> Poll<T>(Func<T?> probe, Func<T?, bool> until, CancellationToken cancellationToken)
    {
        for (int i = 0; i < 100; i++)
        {
            T? value = probe();
            if (until(value))
            {
                return value;
            }

            await Task.Delay(50, cancellationToken);
        }

        return probe();
    }
}
