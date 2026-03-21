using Granit.Localization.Options;
using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

public sealed class LocalizationOverridesCacheOptionsTests
{
    [Fact]
    public void CacheTtl_DefaultsToFiveMinutes()
    {
        LocalizationOverridesCacheOptions options = new();

        options.CacheTtl.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void CacheTtl_CanBeChanged()
    {
        LocalizationOverridesCacheOptions options = new()
        {
            CacheTtl = TimeSpan.FromMinutes(15),
        };

        options.CacheTtl.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void CacheTtl_CanBeSetToZero()
    {
        LocalizationOverridesCacheOptions options = new()
        {
            CacheTtl = TimeSpan.Zero,
        };

        options.CacheTtl.ShouldBe(TimeSpan.Zero);
    }
}
