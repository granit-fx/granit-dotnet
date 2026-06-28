using Granit.Geocoding.Photon.Options;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Photon.Tests;

public sealed class PhotonGeocodingOptionsValidatorTests
{
    private readonly PhotonGeocodingOptionsValidator _sut = new();

    [Fact]
    public void Validate_DefaultOptions_Succeeds() =>
        _sut.Validate(null, new PhotonGeocodingOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_NoUserAgent_Succeeds_BecausePhotonDoesNotRequireOne() =>
        _sut.Validate(null, new PhotonGeocodingOptions { UserAgent = null }).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_EmptyProviderName_Fails()
    {
        PhotonGeocodingOptions options = new() { ProviderName = "" };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RelativeBaseAddress_Fails()
    {
        PhotonGeocodingOptions options = new() { BaseAddress = new Uri("/x", UriKind.Relative) };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonHttpScheme_Fails()
    {
        PhotonGeocodingOptions options = new() { BaseAddress = new Uri("ftp://example.com") };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlaintextHttpToRemoteHost_Fails()
    {
        PhotonGeocodingOptions options = new() { BaseAddress = new Uri("http://photon.komoot.io") };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlaintextHttpToLoopback_Succeeds()
    {
        PhotonGeocodingOptions options = new() { BaseAddress = new Uri("http://localhost:2322") };
        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveTimeout_Fails()
    {
        PhotonGeocodingOptions options = new() { Timeout = TimeSpan.Zero };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveMaxResponseSize_Fails()
    {
        PhotonGeocodingOptions options = new() { MaxResponseSizeBytes = 0 };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveRateLimit_Fails()
    {
        PhotonGeocodingOptions options = new() { RateLimitPerSecond = 0 };
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void SectionName_IsNamespaceAlignedHierarchicalPath() =>
        PhotonGeocodingOptions.SectionName.ShouldBe("Geocoding:Photon");
}
