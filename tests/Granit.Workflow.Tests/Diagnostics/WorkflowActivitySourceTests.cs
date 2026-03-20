using System.Diagnostics;
using Granit.Workflow.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests.Diagnostics;

public sealed class WorkflowActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public WorkflowActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == WorkflowActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_IsGranitWorkflow() =>
        WorkflowActivitySource.Name.ShouldBe("Granit.Workflow");

    [Fact]
    public void StartActivity_Transition_ReturnsActivity()
    {
        using Activity? activity = WorkflowActivitySource.Source.StartActivity(
            WorkflowActivitySource.Transition);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("workflow.transition");
    }
}
