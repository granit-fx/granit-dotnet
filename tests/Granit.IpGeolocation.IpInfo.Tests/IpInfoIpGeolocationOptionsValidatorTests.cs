using Granit.IpGeolocation.IpInfo.Options;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.IpInfo.Tests;

public sealed class IpInfoIpGeolocationOptionsValidatorTests
{
    private readonly IpInfoIpGeolocationOptionsValidator _sut = new();

    [Fact]
    public void Validate_Defaults_Succeeds() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_EmptyProviderName_Fails() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { ProviderName = "" }).Failed.ShouldBeTrue();

    [Fact]
    public void Validate_RelativeBaseAddress_Fails() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { BaseAddress = new Uri("/x", UriKind.Relative) })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_NonHttpScheme_Fails() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { BaseAddress = new Uri("ftp://example.com") })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_PlaintextHttpToRemoteHost_Fails() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { BaseAddress = new Uri("http://ipinfo.io") })
            .Failed.ShouldBeTrue();

    [Fact]
    public void Validate_PlaintextHttpToLoopback_Succeeds() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { BaseAddress = new Uri("http://localhost:9000") })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void SectionName_IsNamespaceAlignedHierarchicalPath() =>
        IpInfoIpGeolocationOptions.SectionName.ShouldBe("IpGeolocation:IpInfo");

    [Fact]
    public void Validate_NonPositiveTimeout_Fails() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { Timeout = TimeSpan.Zero }).Failed.ShouldBeTrue();

    [Fact]
    public void Validate_NonPositiveMaxResponseSize_Fails() =>
        _sut.Validate(null, new IpInfoIpGeolocationOptions { MaxResponseSizeBytes = 0 }).Failed.ShouldBeTrue();
}
