using Granit.Identity.Federated.GoogleCloud.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GoogleCloudIdentityProviderCapabilitiesTests
{
    private readonly GoogleCloudIdentityProviderCapabilities _sut = new();

    [Fact]
    public void ProviderName_IsGoogleCloudIdentityPlatform() =>
        _sut.ProviderName.ShouldBe("Google Cloud Identity Platform");

    [Fact]
    public void SupportsIndividualSessionTermination_IsFalse() =>
        _sut.SupportsIndividualSessionTermination.ShouldBeFalse();

    [Fact]
    public void SupportsNativePasswordResetEmail_IsTrue() =>
        _sut.SupportsNativePasswordResetEmail.ShouldBeTrue();

    [Fact]
    public void SupportsGroupHierarchy_IsFalse() =>
        _sut.SupportsGroupHierarchy.ShouldBeFalse();

    [Fact]
    public void SupportsCustomAttributes_IsTrue() =>
        _sut.SupportsCustomAttributes.ShouldBeTrue();

    [Fact]
    public void MaxCustomAttributes_Is100() =>
        _sut.MaxCustomAttributes.ShouldBe(100);

    [Fact]
    public void SupportsCredentialVerification_IsTrue() =>
        _sut.SupportsCredentialVerification.ShouldBeTrue();

    [Fact]
    public void SupportsUserCreation_IsTrue() =>
        _sut.SupportsUserCreation.ShouldBeTrue();
}
