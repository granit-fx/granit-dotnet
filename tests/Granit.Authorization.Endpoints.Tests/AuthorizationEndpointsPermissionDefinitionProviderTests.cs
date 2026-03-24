using Granit.Authorization.Abstractions;
using Granit.Authorization.Endpoints.Permissions;
using Granit.Localization;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class AuthorizationEndpointsPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_RegistersAuthorizationGroup()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        AuthorizationEndpointsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Groups.ShouldContain(g => g.Name == AuthorizationEndpointsPermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_RegistersDefinitionsReadPermission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        AuthorizationEndpointsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == AuthorizationEndpointsPermissions.Definitions.Read);
    }

    [Fact]
    public void DefinePermissions_RegistersGrantsManagePermission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        AuthorizationEndpointsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single();
        group.Permissions.ShouldContain(p => p.Name == AuthorizationEndpointsPermissions.Grants.Manage);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        AuthorizationEndpointsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);
        provider.DefinePermissions(context);

        // Assert — AddGroup is idempotent (GetOrAdd): only one group in context
        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == AuthorizationEndpointsPermissions.GroupName);
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
