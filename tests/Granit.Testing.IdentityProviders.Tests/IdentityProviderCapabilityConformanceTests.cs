using Granit.Identity;
using Granit.Identity.Models;
using Granit.Testing.IdentityProviders.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Testing.IdentityProviders.Tests;

public sealed class IdentityProviderCapabilityConformanceTests
{
    // A provider that implements the client-role facet, used when a capability claims client-role support.
    private sealed class ClientRoleCapableProvider : IIdentityClientRoleManager
    {
        public Task<IReadOnlyList<string>> GetClientsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IdentityRole>> GetClientRolesAsync(string clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IdentityRole>> GetUserClientRolesAsync(string userId, string clientId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityRole> CreateClientRoleAsync(string clientId, string name, string? description, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AssignClientRoleAsync(string userId, string clientId, string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RemoveClientRoleAsync(string userId, string clientId, string roleName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoClientRoleProvider;

    private sealed class FakeCapabilities : IIdentityProviderCapabilities
    {
        public string ProviderName { get; init; } = "Fake";
        public bool SupportsIndividualSessionTermination { get; init; }
        public bool SupportsNativePasswordResetEmail { get; init; }
        public bool SupportsGroupHierarchy { get; init; }
        public bool SupportsCustomAttributes { get; init; }
        public int MaxCustomAttributes { get; init; }
        public bool SupportsCredentialVerification { get; init; }
        public bool SupportsUserCreation { get; init; }
        public bool SupportsGroupManagement { get; init; }
        public bool IsLocalStore { get; init; }
        public bool SupportsClientRoles { get; init; }
        public bool SupportsClientRoleWrites { get; init; }
    }

    [Fact]
    public void Conforming_capabilities_pass()
    {
        FakeCapabilities caps = new()
        {
            SupportsCustomAttributes = true,
            MaxCustomAttributes = 50,
            SupportsClientRoles = true,
            SupportsClientRoleWrites = true,
        };

        Should.NotThrow(() => IdentityProviderCapabilityConformance.AssertConforms(caps, typeof(ClientRoleCapableProvider)));
    }

    [Fact]
    public void Blank_provider_name_is_rejected()
    {
        FakeCapabilities caps = new() { ProviderName = "  " };

        Should.Throw<IdentityProviderConformanceException>(
            () => IdentityProviderCapabilityConformance.AssertConforms(caps, typeof(NoClientRoleProvider)));
    }

    [Fact]
    public void Custom_attributes_flag_and_count_must_agree()
    {
        FakeCapabilities flagWithoutCount = new() { SupportsCustomAttributes = true, MaxCustomAttributes = 0 };
        Should.Throw<IdentityProviderConformanceException>(
            () => IdentityProviderCapabilityConformance.AssertConforms(flagWithoutCount, typeof(NoClientRoleProvider)));

        FakeCapabilities countWithoutFlag = new() { SupportsCustomAttributes = false, MaxCustomAttributes = 5 };
        Should.Throw<IdentityProviderConformanceException>(
            () => IdentityProviderCapabilityConformance.AssertConforms(countWithoutFlag, typeof(NoClientRoleProvider)));
    }

    [Fact]
    public void Client_role_writes_require_client_role_support()
    {
        FakeCapabilities caps = new() { SupportsClientRoleWrites = true, SupportsClientRoles = false };

        Should.Throw<IdentityProviderConformanceException>(
            () => IdentityProviderCapabilityConformance.AssertConforms(caps, typeof(ClientRoleCapableProvider)));
    }

    [Fact]
    public void Claiming_client_roles_without_implementing_the_facet_is_rejected()
    {
        FakeCapabilities caps = new() { SupportsClientRoles = true };

        IdentityProviderConformanceException ex = Should.Throw<IdentityProviderConformanceException>(
            () => IdentityProviderCapabilityConformance.AssertConforms(caps, typeof(NoClientRoleProvider)));

        ex.Message.ShouldContain("IIdentityClientRoleManager");
    }
}
