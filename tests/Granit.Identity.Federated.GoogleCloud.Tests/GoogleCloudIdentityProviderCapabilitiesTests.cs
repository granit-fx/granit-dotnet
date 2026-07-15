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
    public void SupportsNativePasswordResetEmail_IsFalse() =>
        // Admin SDK generates reset links but never sends the email.
        _sut.SupportsNativePasswordResetEmail.ShouldBeFalse();

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
    public void SupportsCredentialVerification_IsFalse() =>
        // Admin SDK exposes no password-verify API (needs the REST API + Web API key).
        _sut.SupportsCredentialVerification.ShouldBeFalse();

    [Fact]
    public void SupportsUserCreation_IsTrue() =>
        _sut.SupportsUserCreation.ShouldBeTrue();

    [Fact]
    public void SupportsGroupManagement_IsFalse() =>
        _sut.SupportsGroupManagement.ShouldBeFalse();
}
