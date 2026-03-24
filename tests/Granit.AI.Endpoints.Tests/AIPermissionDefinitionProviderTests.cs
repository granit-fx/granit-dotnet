using Granit.AI.Endpoints.Permissions;
using Granit.Authorization.Abstractions;
using Granit.Localization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AI.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="AIPermissionDefinitionProvider"/> correctly
/// registers the AI permission group and all permissions.
/// </summary>
public sealed class AIPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_AI_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(AIPermissions.GroupName);
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_Workspaces_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(AIPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == AIPermissions.Workspaces.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Workspaces_Manage_permission()
    {
        // Arrange
        PermissionGroup group = new(AIPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == AIPermissions.Workspaces.Manage);
    }

    [Fact]
    public void DefinePermissions_adds_Usage_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(AIPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == AIPermissions.Usage.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Chat_Execute_permission()
    {
        // Arrange
        PermissionGroup group = new(AIPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == AIPermissions.Chat.Execute);
    }

    [Fact]
    public void DefinePermissions_adds_Embeddings_Execute_permission()
    {
        // Arrange
        PermissionGroup group = new(AIPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == AIPermissions.Embeddings.Execute);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_five_permissions()
    {
        // Arrange
        PermissionGroup group = new(AIPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(AIPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        AIPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(5);
    }
}
