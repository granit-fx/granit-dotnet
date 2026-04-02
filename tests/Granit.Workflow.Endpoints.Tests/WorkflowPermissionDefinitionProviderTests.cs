using Granit.Authorization;
using Granit.Localization;
using Granit.Workflow.Endpoints.Permissions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

/// <summary>
/// Verifies that <see cref="WorkflowPermissionDefinitionProvider"/> correctly
/// registers the Workflow permission group and the History permission.
/// </summary>
public sealed class WorkflowPermissionDefinitionProviderTests
{
    [Fact]
    public void DefinePermissions_creates_Workflow_group()
    {
        // Arrange
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        PermissionGroup group = new(WorkflowPermissions.GroupName);
        context.AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WorkflowPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        context.Received(1).AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>());
    }

    [Fact]
    public void DefinePermissions_adds_History_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(WorkflowPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WorkflowPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == WorkflowPermissions.History.Read);
    }

    [Fact]
    public void DefinePermissions_History_Read_permission_has_display_name()
    {
        // Arrange
        PermissionGroup group = new(WorkflowPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WorkflowPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        PermissionDefinition historyPermission = group.Permissions
            .Single(p => p.Name == WorkflowPermissions.History.Read);
        historyPermission.DisplayName.ShouldNotBeNull();
    }

    [Fact]
    public void DefinePermissions_adds_Transitions_Read_permission()
    {
        // Arrange
        PermissionGroup group = new(WorkflowPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WorkflowPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == WorkflowPermissions.Transitions.Read);
    }

    [Fact]
    public void DefinePermissions_adds_Transitions_Execute_permission()
    {
        // Arrange
        PermissionGroup group = new(WorkflowPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WorkflowPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.ShouldContain(p => p.Name == WorkflowPermissions.Transitions.Execute);
    }

    [Fact]
    public void DefinePermissions_registers_exactly_three_permissions()
    {
        // Arrange
        PermissionGroup group = new(WorkflowPermissions.GroupName);
        IPermissionDefinitionContext context = Substitute.For<IPermissionDefinitionContext>();
        context.AddGroup(WorkflowPermissions.GroupName, Arg.Any<LocalizableString>()).Returns(group);

        WorkflowPermissionDefinitionProvider provider = new();

        // Act
        provider.DefinePermissions(context);

        // Assert
        group.Permissions.Count.ShouldBe(3);
    }
}
