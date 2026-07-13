using Granit.Authorization;
using Granit.Localization;
using Granit.Notifications.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="NotificationsPermissionDefinitionProvider"/> correctly
/// registers the Notifications permission group and Read/Manage permissions.
/// </summary>
public sealed class NotificationsPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_Notifications_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(NotificationsPermissions.GroupName);
        context.AddGroup(NotificationsPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        NotificationsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(NotificationsPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(NotificationsPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(NotificationsPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        NotificationsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == NotificationsPermissions.UserNotifications.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(NotificationsPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(NotificationsPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        NotificationsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == NotificationsPermissions.UserNotifications.Manage);
    }

    [Fact]
    public void DefinePermissions_Read_permission_has_display_name()
    {
        // Arrange
        PermissionGroup group = new(NotificationsPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(NotificationsPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        NotificationsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionDefinition readPermission = group.Permissions
            .Single(p => p.Name == NotificationsPermissions.UserNotifications.Read);
        readPermission.DisplayName.ShouldNotBeNull();
    }

    [Fact]
    public void DefinePermissions_registers_exactly_two_permissions()
    {
        // Arrange
        PermissionGroup group = new(NotificationsPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(NotificationsPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        NotificationsPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(2);
    }
}
