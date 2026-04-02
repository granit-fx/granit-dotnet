using Granit.Authorization;
using Granit.Identity.Endpoints.Permissions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Tests that the identity permission definition provider registers all expected permissions.
/// </summary>
public sealed class IdentityPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersIdentityGroup()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        IdentityPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Groups.ShouldContain(g => g.Name == IdentityPermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_RegistersAllPermissions()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        IdentityPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single();
        group.Permissions.Count.ShouldBe(11);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Users.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Users.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Users.Sync);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Users.Delete);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Roles.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Roles.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Groups.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Groups.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Sessions.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Sessions.Manage);
        group.Permissions.ShouldContain(p => p.Name == IdentityPermissions.Passwords.Manage);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        IdentityPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);
        provider.DefinePermissions(context);

        // Assert — AddGroup is idempotent (GetOrAdd): only one group in context
        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == IdentityPermissions.GroupName);
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
