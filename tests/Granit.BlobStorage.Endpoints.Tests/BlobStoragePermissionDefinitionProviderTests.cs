using Granit.Authorization.Abstractions;
using Granit.BlobStorage.Endpoints.Permissions;
using Granit.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="BlobStoragePermissionDefinitionProvider"/> correctly
/// registers the BlobStorage permission group and all Administration permissions.
/// </summary>
public sealed class BlobStoragePermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_BlobStorage_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(BlobStoragePermissions.GroupName);
        context.AddGroup(BlobStoragePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        BlobStoragePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(BlobStoragePermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(BlobStoragePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(BlobStoragePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        BlobStoragePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == BlobStoragePermissions.Administration.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(BlobStoragePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(BlobStoragePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        BlobStoragePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == BlobStoragePermissions.Administration.Manage);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_two_permissions()
    {
        // Arrange
        PermissionGroup group = new(BlobStoragePermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(BlobStoragePermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        BlobStoragePermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(2);
    }
}
