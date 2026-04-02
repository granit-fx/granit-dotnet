using Granit.Authorization;
using Granit.DataExchange.Endpoints.Permissions;
using Granit.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="DataExchangePermissionDefinitionProvider"/> correctly
/// registers the DataExchange permission group and all Import/Export permissions.
/// </summary>
public sealed class DataExchangePermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_DataExchange_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(DataExchangePermissions.GroupName);
        context.AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataExchangePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Imports_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(DataExchangePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataExchangePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == DataExchangePermissions.Imports.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Imports_Execute_permission()
    {
        // Arrange
        PermissionGroup group = new(DataExchangePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataExchangePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == DataExchangePermissions.Imports.Execute);
    }

    [Fact]
    public void DefinePermissions_adds_Exports_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(DataExchangePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataExchangePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == DataExchangePermissions.Exports.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Exports_Execute_permission()
    {
        // Arrange
        PermissionGroup group = new(DataExchangePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataExchangePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == DataExchangePermissions.Exports.Execute);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_four_permissions()
    {
        // Arrange
        PermissionGroup group = new(DataExchangePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(DataExchangePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        DataExchangePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(4);
    }
}
