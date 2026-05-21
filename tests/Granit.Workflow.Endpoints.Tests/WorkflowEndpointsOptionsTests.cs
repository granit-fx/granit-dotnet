using Granit.Workflow.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

/// <summary>
/// Verifies default values and mutability of <see cref="WorkflowEndpointsOptions"/>.
/// </summary>
public sealed class WorkflowEndpointsOptionsTests
{
    [Fact]
    public void SectionName_is_WorkflowEndpoints() =>
        WorkflowEndpointsOptions.SectionName.ShouldBe("Workflow:Endpoints");

    [Fact]
    public void Default_RoutePrefix_is_workflow()
    {
        WorkflowEndpointsOptions options = new();
        options.RoutePrefix.ShouldBe("workflow");
    }

    [Fact]
    public void Default_TagName_is_Workflow()
    {
        WorkflowEndpointsOptions options = new();
        options.TagName.ShouldBe("Workflow");
    }

    [Fact]
    public void Properties_are_mutable()
    {
        WorkflowEndpointsOptions options = new()
        {
            RoutePrefix = "wf",
            TagName = "WF",
        };

        options.RoutePrefix.ShouldBe("wf");
        options.TagName.ShouldBe("WF");
    }
}
