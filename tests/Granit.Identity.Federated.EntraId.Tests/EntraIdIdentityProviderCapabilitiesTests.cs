using Granit.Identity.Federated.EntraId.Extensions;
using Granit.Identity.Federated.EntraId.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdIdentityProviderCapabilitiesTests
{
    private readonly EntraIdIdentityProviderCapabilities _capabilities = new();

    [Fact]
    public void ProviderName_ReturnsEntraId() =>
        _capabilities.ProviderName.ShouldBe("Entra ID");

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
    public void SupportsCustomAttributes_ReturnsTrue() =>
        _capabilities.SupportsCustomAttributes.ShouldBeTrue();

    [Fact]
    public void MaxCustomAttributes_Returns15() =>
        _capabilities.MaxCustomAttributes.ShouldBe(15);

    [Fact]
    public void SupportsCredentialVerification_ReturnsTrue() =>
        _capabilities.SupportsCredentialVerification.ShouldBeTrue();

    [Fact]
    public void SupportsUserCreation_ReturnsTrue() =>
        _capabilities.SupportsUserCreation.ShouldBeTrue();

    [Fact]
    public void SupportsGroupManagement_ReturnsFalse() =>
        _capabilities.SupportsGroupManagement.ShouldBeFalse();

    [Fact]
    public void ImplementsIIdentityProviderCapabilities() =>
        _capabilities.ShouldBeAssignableTo<IIdentityProviderCapabilities>();

    [Fact]
    public void AddGranitIdentityEntraId_RegistersEntraIdCapabilities()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

        builder.Services.AddGranitIdentityEntraId();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProviderCapabilities));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(EntraIdIdentityProviderCapabilities));
    }
}
