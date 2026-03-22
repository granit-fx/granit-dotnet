using Granit.Identity.Federated.Cognito.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class CognitoIdentityProviderCapabilitiesTests
{
    private readonly CognitoIdentityProviderCapabilities _capabilities = new();

    [Fact]
    public void ProviderName_ReturnsCognito() =>
        _capabilities.ProviderName.ShouldBe("Cognito");

    [Fact]
    public void SupportsIndividualSessionTermination_ReturnsFalse() =>
        _capabilities.SupportsIndividualSessionTermination.ShouldBeFalse();

    [Fact]
    public void SupportsNativePasswordResetEmail_ReturnsTrue() =>
        _capabilities.SupportsNativePasswordResetEmail.ShouldBeTrue();

    [Fact]
    public void SupportsGroupHierarchy_ReturnsFalse() =>
        _capabilities.SupportsGroupHierarchy.ShouldBeFalse();

    [Fact]
    public void SupportsCustomAttributes_ReturnsTrue() =>
        _capabilities.SupportsCustomAttributes.ShouldBeTrue();

    [Fact]
    public void MaxCustomAttributes_Returns50() =>
        _capabilities.MaxCustomAttributes.ShouldBe(50);

    [Fact]
    public void SupportsCredentialVerification_ReturnsTrue() =>
        _capabilities.SupportsCredentialVerification.ShouldBeTrue();

    [Fact]
    public void SupportsUserCreation_ReturnsTrue() =>
        _capabilities.SupportsUserCreation.ShouldBeTrue();

    [Fact]
    public void Implements_IIdentityProviderCapabilities() =>
        _capabilities.ShouldBeAssignableTo<IIdentityProviderCapabilities>();
}
