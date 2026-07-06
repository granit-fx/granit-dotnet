using Granit.Caching.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class CachingOptionsTests
{
    [Fact]
    public void SectionName_IsCache() => CachingOptions.SectionName.ShouldBe("Cache");

    [Fact]
    public void Defaults_KeyPrefix_IsDd()
    {
        CachingOptions options = new();

        options.KeyPrefix.ShouldBe("dd");
    }

    [Fact]
    public void Defaults_DefaultAbsoluteExpirationRelativeToNow_IsOneHour()
    {
        CachingOptions options = new();

        options.DefaultAbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Defaults_EncryptValues_IsFalse()
    {
        CachingOptions options = new();

        options.EncryptValues.ShouldBeFalse();
    }

    [Fact]
    public void Defaults_JsonOptions_IsNull()
    {
        CachingOptions options = new();

        options.JsonOptions.ShouldBeNull();
    }
}
