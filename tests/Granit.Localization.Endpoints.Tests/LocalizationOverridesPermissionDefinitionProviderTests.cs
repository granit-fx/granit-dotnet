using Granit.Authorization.Abstractions;
using Granit.Core.Localization;
using Granit.Localization.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="LocalizationOverridesPermissionDefinitionProvider"/> correctly
/// registers the Localization permission group and Read/Manage permissions.
/// </summary>
public sealed class LocalizationOverridesPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_Localization_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(LocalizationOverridesPermissions.GroupName);
        context.AddGroup(LocalizationOverridesPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        LocalizationOverridesPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(LocalizationOverridesPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(LocalizationOverridesPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(LocalizationOverridesPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        LocalizationOverridesPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == LocalizationOverridesPermissions.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(LocalizationOverridesPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(LocalizationOverridesPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        LocalizationOverridesPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == LocalizationOverridesPermissions.Manage);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_two_permissions()
    {
        // Arrange
        PermissionGroup group = new(LocalizationOverridesPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(LocalizationOverridesPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        LocalizationOverridesPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(2);
    }
}
