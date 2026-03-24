using Granit.Authorization.Abstractions;
using Granit.Localization;
using Granit.Templating.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="TemplatingPermissionDefinitionProvider"/> correctly
/// registers the Templating permission group and Read/Manage permissions.
/// </summary>
public sealed class TemplatingPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_Templating_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(TemplatingPermissions.GroupName);
        context.AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        TemplatingPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(TemplatingPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        TemplatingPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == TemplatingPermissions.Templates.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(TemplatingPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        TemplatingPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == TemplatingPermissions.Templates.Manage);
    }

    [Fact]
    public void DefinePermissions_adds_Categories_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(TemplatingPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        TemplatingPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == TemplatingPermissions.Categories.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Categories_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(TemplatingPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        TemplatingPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == TemplatingPermissions.Categories.Manage);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_four_permissions()
    {
        // Arrange
        PermissionGroup group = new(TemplatingPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(TemplatingPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        TemplatingPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(4);
    }
}
