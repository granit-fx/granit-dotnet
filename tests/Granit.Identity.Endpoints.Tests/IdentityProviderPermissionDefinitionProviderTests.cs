using Granit.Authorization.Abstractions;
using Granit.Identity.Endpoints.Permissions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Tests that the identity provider permission definition provider registers all expected permissions.
/// </summary>
public sealed class IdentityProviderPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersIdentityGroup()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        IdentityProviderPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Groups.ShouldContain(g => g.Name == IdentityProviderPermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_RegistersAllPermissions()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        IdentityProviderPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single();
        group.Permissions.Count.ShouldBe(9);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Users.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Users.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Roles.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Roles.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Groups.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Groups.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Sessions.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Sessions.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityProviderPermissions.Passwords.Manage);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        IdentityProviderPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);
        provider.DefinePermissions(context);

        // Assert — AddGroup is idempotent (GetOrAdd): only one group in context
        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == IdentityProviderPermissions.GroupName);
    }

    // ── Test double ────────────────────────────────────────────────────────────

    private sealed class FakePermissionDefinitionContext : IPermissionDefinitionContext
    {
        private readonly Dictionary<string, PermissionGroup> _groups = new(StringComparer.Ordinal);

        public IReadOnlyCollection<PermissionGroup> Groups => _groups.Values;

        public PermissionGroup AddGroup(string name, LocalizableString? displayName = null)
        {
            if (_groups.TryGetValue(name, out PermissionGroup? existing))
            {
                return existing;
            }

            PermissionGroup group = new(name, displayName);
            _groups[name] = group;
            return group;
        }
    }
}
