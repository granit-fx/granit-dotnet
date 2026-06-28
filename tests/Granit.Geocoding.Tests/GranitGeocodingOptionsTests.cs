using Granit.Geocoding.Options;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Tests;

public sealed class GranitGeocodingOptionsTests
{
    [Fact]
    public void SectionName_IsGeocoding() =>
        GranitGeocodingOptions.SectionName.ShouldBe("Geocoding");

    [Fact]
    public void Defaults_FavourLongSuccessAndShortFailureCaching()
    {
        GranitGeocodingOptions options = new();

        options.ProviderOrder.ShouldBeEmpty();
        options.SuccessCacheDuration.ShouldBe(TimeSpan.FromDays(90));
        options.FailureCacheDuration.ShouldBe(TimeSpan.FromDays(1));
        options.FailureCacheDuration.ShouldBeLessThan(options.SuccessCacheDuration);
    }
}
