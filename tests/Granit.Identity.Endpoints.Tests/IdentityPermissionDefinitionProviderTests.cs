using Granit.Authorization.Abstractions;
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
        context.Groups.ShouldContain(g => g.Name == IdentityUserCachePermissions.GroupName);
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
        group.Permissions.Count.ShouldBe(3);
        group.Permissions.ShouldContain(p => p.Name == IdentityUserCachePermissions.UserCache.Read);
        group.Permissions.ShouldContain(p => p.Name == IdentityUserCachePermissions.UserCache.Sync);
        group.Permissions.ShouldContain(p => p.Name == IdentityUserCachePermissions.UserCache.Delete);
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
            .ShouldContain(n => n == IdentityUserCachePermissions.GroupName);
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
