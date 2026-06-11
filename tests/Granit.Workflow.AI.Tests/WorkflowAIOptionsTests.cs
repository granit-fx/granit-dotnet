using Granit.Workflow.AI.Options;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Tests for <see cref="WorkflowAIOptions"/> configuration class.
/// </summary>
public sealed class WorkflowAIOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAIWorkflow() =>
        WorkflowAIOptions.SectionName.ShouldBe("Workflow:AI");

    [Fact]
    public void Default_WorkspaceName_ShouldBeDefault()
    {
        WorkflowAIOptions options = new();
        options.WorkspaceName.ShouldBe("default");
    }

    [Fact]
    public void Default_TimeoutSeconds_ShouldBe10()
    {
        WorkflowAIOptions options = new();
        options.TimeoutSeconds.ShouldBe(10);
    }

    [Fact]
    public void Default_AutoApprovalThreshold_ShouldBe03()
    {
        WorkflowAIOptions options = new();
        options.AutoApprovalThreshold.ShouldBe(0.3);
    }

    [Fact]
    public void Properties_AreMutable()
    {
        WorkflowAIOptions options = new()
        {
            WorkspaceName = "custom",
            TimeoutSeconds = 30,
            AutoApprovalThreshold = 0.5,
        };

        options.WorkspaceName.ShouldBe("custom");
        options.TimeoutSeconds.ShouldBe(30);
        options.AutoApprovalThreshold.ShouldBe(0.5);
    }
}
