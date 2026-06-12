using Granit.IpGeolocation.MaxMind.Internal;
using Granit.IpGeolocation.MaxMind.Options;
using MaxMind.Db;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.MaxMind.Tests;

public sealed class MaxMindDatabaseProviderTests
{
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
}
