using Granit.Geocoding.Nominatim.Options;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Nominatim.Tests;

public sealed class NominatimGeocodingOptionsValidatorTests
{
    private readonly NominatimGeocodingOptionsValidator _sut = new();

    private static NominatimGeocodingOptions Valid() => new() { UserAgent = "MyApp/1.0 (ops@example.com)" };

    [Fact]
    public void Validate_ValidOptions_Succeeds() =>
        _sut.Validate(null, Valid()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_DefaultOptions_FailsBecauseUserAgentIsMandatory() =>
        _sut.Validate(null, new NominatimGeocodingOptions()).Failed.ShouldBeTrue();

    [Fact]
    public void Validate_EmptyProviderName_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.ProviderName = "";
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RelativeBaseAddress_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.BaseAddress = new Uri("/x", UriKind.Relative);
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonHttpScheme_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.BaseAddress = new Uri("ftp://example.com");
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlaintextHttpToRemoteHost_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.BaseAddress = new Uri("http://nominatim.openstreetmap.org");
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_PlaintextHttpToLoopback_Succeeds()
    {
        NominatimGeocodingOptions options = Valid();
        options.BaseAddress = new Uri("http://localhost:8080");
        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveTimeout_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.Timeout = TimeSpan.Zero;
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveMaxResponseSize_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.MaxResponseSizeBytes = 0;
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_BlankUserAgent_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.UserAgent = "   ";
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveRateLimit_Fails()
    {
        NominatimGeocodingOptions options = Valid();
        options.RateLimitPerSecond = 0;
        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void SectionName_IsNamespaceAlignedHierarchicalPath() =>
        NominatimGeocodingOptions.SectionName.ShouldBe("Geocoding:Nominatim");
}
