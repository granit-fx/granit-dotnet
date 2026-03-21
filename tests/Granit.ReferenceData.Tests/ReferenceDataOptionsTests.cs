using Granit.ReferenceData.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataOptionsTests
{
    [Fact]
    public void SectionName_Is_ReferenceData() => ReferenceDataOptions.SectionName.ShouldBe("ReferenceData");

    [Fact]
    public void Default_CacheTimeToLive_Is_OneHour()
    {
        ReferenceDataOptions options = new();

        options.CacheTimeToLive.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void CacheTimeToLive_CanBeSet()
    {
        ReferenceDataOptions options = new()
        {
            CacheTimeToLive = TimeSpan.FromMinutes(30),
        };

        options.CacheTimeToLive.ShouldBe(TimeSpan.FromMinutes(30));
    }
}
