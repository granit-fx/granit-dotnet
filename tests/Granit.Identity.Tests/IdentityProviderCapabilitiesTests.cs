using Granit.Identity.Extensions;
using Granit.Identity.Internal;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests;

public sealed class IdentityProviderCapabilitiesTests
{
    private readonly NullIdentityProviderCapabilities _capabilities = new();

    [Fact]
    public void ProviderName_ReturnsNone() =>
        _capabilities.ProviderName.ShouldBe("None");

    [Fact]
    public void SupportsIndividualSessionTermination_ReturnsFalse() =>
        _capabilities.SupportsIndividualSessionTermination.ShouldBeFalse();

    [Fact]
    public void SupportsNativePasswordResetEmail_ReturnsFalse() =>
        _capabilities.SupportsNativePasswordResetEmail.ShouldBeFalse();

    [Fact]
    public void SupportsGroupHierarchy_ReturnsFalse() =>
        _capabilities.SupportsGroupHierarchy.ShouldBeFalse();

    [Fact]
    public void SupportsCustomAttributes_ReturnsFalse() =>
        _capabilities.SupportsCustomAttributes.ShouldBeFalse();

    [Fact]
    public void MaxCustomAttributes_ReturnsZero() =>
        _capabilities.MaxCustomAttributes.ShouldBe(0);

    [Fact]
    public void SupportsCredentialVerification_ReturnsFalse() =>
        _capabilities.SupportsCredentialVerification.ShouldBeFalse();

    [Fact]
    public void SupportsUserCreation_ReturnsFalse() =>
        _capabilities.SupportsUserCreation.ShouldBeFalse();

    [Fact]
    public void SupportsGroupManagement_ReturnsFalse() =>
        _capabilities.SupportsGroupManagement.ShouldBeFalse();

    [Fact]
    public void ImplementsIIdentityProviderCapabilities() =>
        _capabilities.ShouldBeAssignableTo<IIdentityProviderCapabilities>();

    [Fact]
    public void AddGranitIdentity_RegistersNullCapabilitiesAsDefault()
    {
        ServiceCollection services = new();

        services.AddGranitIdentity();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IIdentityProviderCapabilities capabilities = scope.ServiceProvider
            .GetRequiredService<IIdentityProviderCapabilities>();

        capabilities.ShouldBeOfType<NullIdentityProviderCapabilities>();
        capabilities.ProviderName.ShouldBe("None");
    }

    [Fact]
    public void AddGranitIdentity_RegistersCapabilitiesAsScoped()
    {
        ServiceCollection services = new();

        services.AddGranitIdentity();

        ServiceDescriptor descriptor = services.Single(
            d => d.ServiceType == typeof(IIdentityProviderCapabilities));
        descriptor.ImplementationType.ShouldBe(typeof(NullIdentityProviderCapabilities));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitIdentity_CalledTwice_RegistersCapabilitiesOnce()
    {
        ServiceCollection services = new();

        services.AddGranitIdentity();
        services.AddGranitIdentity();

        services.Count(d => d.ServiceType == typeof(IIdentityProviderCapabilities)).ShouldBe(1);
    }
}
