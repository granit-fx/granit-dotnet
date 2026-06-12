using Granit.IpGeolocation.IpApi.Options;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.IpApi.Tests;

public sealed class IpApiIpGeolocationOptionsValidatorTests
{
    private readonly IpApiIpGeolocationOptionsValidator _sut = new();

    [Fact]
    public void Validate_Defaults_Succeeds() =>
        _sut.Validate(null, new IpApiIpGeolocationOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_EmptyProviderName_Fails() =>
        _sut.Validate(null, new IpApiIpGeolocationOptions { ProviderName = "" }).Failed.ShouldBeTrue();

    [Fact]
    public void Validate_RelativeBaseAddress_Fails() =>
        _sut.Validate(null, new IpApiIpGeolocationOptions { BaseAddress = new Uri("/x", UriKind.Relative) })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_NonHttpScheme_Fails() =>
        _sut.Validate(null, new IpApiIpGeolocationOptions { BaseAddress = new Uri("ftp://example.com") })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_NonPositiveTimeout_Fails() =>
        _sut.Validate(null, new IpApiIpGeolocationOptions { Timeout = TimeSpan.Zero }).Failed.ShouldBeTrue();

    [Fact]
    public void Validate_NonPositiveMaxResponseSize_Fails() =>
        _sut.Validate(null, new IpApiIpGeolocationOptions { MaxResponseSizeBytes = 0 }).Failed.ShouldBeTrue();
}
