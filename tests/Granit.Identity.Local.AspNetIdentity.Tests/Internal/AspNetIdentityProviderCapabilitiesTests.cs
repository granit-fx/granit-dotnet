using Granit.Identity.Local.AspNetIdentity.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Internal;

public sealed class AspNetIdentityProviderCapabilitiesTests
{
    private readonly AspNetIdentityProviderCapabilities _sut = new();

    [Fact]
    public void ProviderName_IsAspNetIdentity() =>
        _sut.ProviderName.ShouldBe("AspNetIdentity");

    [Fact]
    public void IsLocalStore_IsTrue() =>
        _sut.IsLocalStore.ShouldBeTrue();

    [Fact]
    public void SupportsCredentialVerification_IsTrue() =>
        _sut.SupportsCredentialVerification.ShouldBeTrue();

    [Fact]
    public void SupportsUserCreation_IsTrue() =>
        _sut.SupportsUserCreation.ShouldBeTrue();

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
    public void MaxCustomAttributes_IsIntMaxValue() =>
        _sut.MaxCustomAttributes.ShouldBe(int.MaxValue);

    [Fact]
    public void SupportsGroupManagement_IsFalse() =>
        _sut.SupportsGroupManagement.ShouldBeFalse();
}
