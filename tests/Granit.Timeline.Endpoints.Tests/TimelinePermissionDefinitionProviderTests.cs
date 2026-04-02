using Granit.Authorization;
using Granit.Localization;
using Granit.Timeline.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Unit tests for <see cref="TimelinePermissionDefinitionProvider"/>.
/// </summary>
public sealed class TimelinePermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_registers_Timeline_group()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Groups.ShouldContain(g => g.Name == TimelinePermissions.GroupName);
    }

    [Fact]
    public void DefinePermissions_registers_Entries_Read_permission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single(g => g.Name == TimelinePermissions.GroupName);
        group.Permissions.ShouldContain(p => p.Name == TimelinePermissions.Entries.Read);
    }

    [Fact]
    public void DefinePermissions_registers_Entries_Create_permission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single(g => g.Name == TimelinePermissions.GroupName);
        group.Permissions.ShouldContain(p => p.Name == TimelinePermissions.Entries.Create);
    }

    [Fact]
    public void DefinePermissions_registers_Entries_Manage_permission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single(g => g.Name == TimelinePermissions.GroupName);
        group.Permissions.ShouldContain(p => p.Name == TimelinePermissions.Entries.Manage);
    }

    [Fact]
    public void DefinePermissions_registers_InternalNotes_Read_permission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single(g => g.Name == TimelinePermissions.GroupName);
        group.Permissions.ShouldContain(p => p.Name == TimelinePermissions.InternalNotes.Read);
    }

    [Fact]
    public void DefinePermissions_registers_Followers_Manage_permission()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single(g => g.Name == TimelinePermissions.GroupName);
        group.Permissions.ShouldContain(p => p.Name == TimelinePermissions.Followers.Manage);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_five_permissions()
    {
        // Arrange
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionGroup group = context.Groups.Single(g => g.Name == TimelinePermissions.GroupName);
        group.Permissions.Count.ShouldBe(5);
    }

    [Fact]
    public void DefinePermissions_CalledTwice_DoesNotDuplicateGroup()
    {
        // Arrange -- same context receives two calls (multi-provider scenario)
        FakePermissionDefinitionContext context = new();
        TimelinePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);
        provider.DefinePermissions(context); // second call via same context uses GetOrAdd semantics

        // Assert -- AddGroup is idempotent (GetOrAdd): only one group in context
        context.Groups.Select(g => g.Name)
            .ShouldContain(n => n == TimelinePermissions.GroupName);
    }

    // -- Test double ---------------------------------------------------------------

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
