// =============================================================================
// Tests - DynamicPermissionPolicyProvider
// =============================================================================
// Vérifie que le provider :
//   - Retourne une Policy avec PermissionRequirement pour une permission connue
//   - Délègue au fallback pour une policy standard ("Authenticated")
//   - Retourne null pour une policy totalement inconnue
// =============================================================================

using Granit.Authorization;
using Granit.Authorization.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class DynamicPermissionPolicyProviderTests
{
    [Fact]
    public async Task GetPolicyAsync_KnownPermission_ReturnsPolicyWithPermissionRequirement()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists("Invoices.Delete").Returns(true);

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("Invoices.Delete");

        // Assert
        policy.ShouldNotBeNull();
        policy!.Requirements.Count.ShouldBe(2);
        policy.Requirements.OfType<PermissionRequirement>().ShouldHaveSingleItem()
            .PermissionName.ShouldBe("Invoices.Delete");
    }

    [Fact]
    public async Task GetPolicyAsync_UnknownPolicyName_ReturnsNull()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists("Unknown.Policy").Returns(false);

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("Unknown.Policy");

        // Assert
        policy.ShouldBeNull();
    }

    [Fact]
    public async Task GetPolicyAsync_StandardAuthenticatedPolicy_DelegatesToFallback()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists("Authenticated").Returns(false); // not a permission name

        AuthorizationOptions authOptions = new();
        authOptions.AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(authOptions),
            manager);

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("Authenticated");

        // Assert
        policy.ShouldNotBeNull("the fallback provider should resolve the registered policy");
    }

    [Fact]
    public async Task GetDefaultPolicyAsync_DelegatesToFallback()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy defaultPolicy = await provider.GetDefaultPolicyAsync();

        // Assert
        defaultPolicy.ShouldNotBeNull();
    }

    // =========================================================================
    // GetFallbackPolicyAsync
    // =========================================================================

    [Fact]
    public async Task GetFallbackPolicyAsync_Default_ReturnsNull()
    {
        // Arrange — default AuthorizationOptions has no fallback policy
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy? fallback = await provider.GetFallbackPolicyAsync();

        // Assert
        fallback.ShouldBeNull();
    }

    [Fact]
    public async Task GetFallbackPolicyAsync_WhenConfigured_ReturnsFallback()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        AuthorizationOptions authOptions = new()
        {
            FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        };

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(authOptions),
            manager);

        // Act
        AuthorizationPolicy? fallback = await provider.GetFallbackPolicyAsync();

        // Assert
        fallback.ShouldNotBeNull();
    }

    // =========================================================================
    // GetPolicyAsync — policy structure details
    // =========================================================================

    [Fact]
    public async Task GetPolicyAsync_KnownPermission_PolicyRequiresAuthenticatedUser()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists("Orders.Create").Returns(true);

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("Orders.Create");

        // Assert — policy must contain DenyAnonymousAuthorizationRequirement
        policy.ShouldNotBeNull();
        policy!.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement>()
            .ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPolicyAsync_KnownPermission_PermissionRequirementContainsCorrectName()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists("BlobStorage.Blobs.Upload").Returns(true);

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy? policy = await provider.GetPolicyAsync("BlobStorage.Blobs.Upload");

        // Assert
        policy.ShouldNotBeNull();
        PermissionRequirement requirement = policy!.Requirements.OfType<PermissionRequirement>().ShouldHaveSingleItem();
        requirement.PermissionName.ShouldBe("BlobStorage.Blobs.Upload");
    }

    [Fact]
    public async Task GetPolicyAsync_MultipleKnownPermissions_EachReturnsSeparatePolicy()
    {
        // Arrange
        IPermissionDefinitionManager manager = Substitute.For<IPermissionDefinitionManager>();
        manager.Exists("Invoices.Read").Returns(true);
        manager.Exists("Invoices.Delete").Returns(true);

        DynamicPermissionPolicyProvider provider = new(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()),
            manager);

        // Act
        AuthorizationPolicy? readPolicy = await provider.GetPolicyAsync("Invoices.Read");
        AuthorizationPolicy? deletePolicy = await provider.GetPolicyAsync("Invoices.Delete");

        // Assert
        readPolicy.ShouldNotBeNull();
        deletePolicy.ShouldNotBeNull();

        readPolicy!.Requirements.OfType<PermissionRequirement>().ShouldHaveSingleItem()
            .PermissionName.ShouldBe("Invoices.Read");
        deletePolicy!.Requirements.OfType<PermissionRequirement>().ShouldHaveSingleItem()
            .PermissionName.ShouldBe("Invoices.Delete");
    }
}
