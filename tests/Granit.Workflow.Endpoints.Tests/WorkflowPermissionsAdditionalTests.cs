using Granit.Workflow.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

/// <summary>
/// Additional tests for <see cref="WorkflowPermissions"/> Transitions constants.
/// </summary>
public sealed class WorkflowPermissionsAdditionalTests
{
    [Fact]
    public void Transitions_Read_is_Workflow_Transitions_Read() =>
        WorkflowPermissions.Transitions.Read.ShouldBe("Workflow.Transitions.Read");

    [Fact]
    public void Transitions_Execute_is_Workflow_Transitions_Execute() =>
        WorkflowPermissions.Transitions.Execute.ShouldBe("Workflow.Transitions.Execute");

    [Fact]
    public void Transitions_Read_starts_with_GroupName() =>
        WorkflowPermissions.Transitions.Read.ShouldStartWith(WorkflowPermissions.GroupName + ".");

    [Fact]
    public void Transitions_Execute_starts_with_GroupName() =>
        WorkflowPermissions.Transitions.Execute.ShouldStartWith(WorkflowPermissions.GroupName + ".");

    [Fact]
    public void All_permissions_follow_three_segment_pattern()
    {
        // Assert — all constants use Group.Resource.Action format
        string[] permissions =
        [
            WorkflowPermissions.History.Read,
            WorkflowPermissions.Transitions.Read,
            WorkflowPermissions.Transitions.Execute,
        ];

        foreach (string permission in permissions)
        {
            string[] segments = permission.Split('.');
            segments.Length.ShouldBe(3, $"Permission '{permission}' must have 3 dot-separated segments.");
        }
    }
}
