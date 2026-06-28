using Granit.Geocoding.Options;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Tests;

public sealed class GranitGeocodingOptionsValidatorTests
{
    private readonly GranitGeocodingOptionsValidator _sut = new();

    [Fact]
    public void Validate_DefaultOptions_Succeeds() =>
        _sut.Validate(null, new GranitGeocodingOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_NonPositiveSuccessCacheDuration_Fails()
    {
        GranitGeocodingOptions options = new() { SuccessCacheDuration = TimeSpan.Zero };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NegativeFailureCacheDuration_Fails()
    {
        GranitGeocodingOptions options = new() { FailureCacheDuration = TimeSpan.FromSeconds(-1) };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }
}
