using Granit.Templating.Workflow.Internal;
using Granit.Workflow;
using Granit.Workflow.Domain;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Templating.Workflow.Tests;

public sealed class WorkflowTemplateTransitionHookTests
{
    // -------------------------------------------------------------------------
    // Test infrastructure
    // -------------------------------------------------------------------------

    private static WorkflowTemplateTransitionHook CreateHook(
        IWorkflowManager<WorkflowLifecycleStatus>? workflowManager = null)
    {
        IWorkflowManager<WorkflowLifecycleStatus> manager = workflowManager ?? CreateDefaultManager();
        return new WorkflowTemplateTransitionHook(manager);
    }

    private static IWorkflowManager<WorkflowLifecycleStatus> CreateDefaultManager()
    {
        IWorkflowManager<WorkflowLifecycleStatus> manager = Substitute.For<IWorkflowManager<WorkflowLifecycleStatus>>();

        manager.TransitionAsync(
                Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<WorkflowLifecycleStatus>(),
                Arg.Any<TransitionContext?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                WorkflowLifecycleStatus from = callInfo.ArgAt<WorkflowLifecycleStatus>(0);
                WorkflowLifecycleStatus to = callInfo.ArgAt<WorkflowLifecycleStatus>(1);

                // Define valid direct transitions (Completed outcome).
                bool isValid = (from, to) is
                    (WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published) or
                    (WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived) or
                    (WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Draft) or
                    (WorkflowLifecycleStatus.PendingReview, WorkflowLifecycleStatus.Published);

                return isValid
                    ? new TransitionResult<WorkflowLifecycleStatus>
                    {
                        Succeeded = true,
                        ResultingState = to,
                        Outcome = TransitionOutcome.Completed,
                    }
                    : new TransitionResult<WorkflowLifecycleStatus>
                    {
                        Succeeded = false,
                        ResultingState = from,
                        Outcome = TransitionOutcome.InvalidTransition,
                    };
            });

        return manager;
    }

    // -------------------------------------------------------------------------
    // IsWorkflowEnabled
    // -------------------------------------------------------------------------

    [Fact]
    public void IsWorkflowEnabled_ReturnsTrue()
    {
        WorkflowTemplateTransitionHook hook = CreateHook();
        hook.IsWorkflowEnabled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // CanTransitionAsync
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, true)]
    [InlineData(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived, true)]
    [InlineData(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Draft, true)]
    [InlineData(WorkflowLifecycleStatus.PendingReview, WorkflowLifecycleStatus.Published, true)]
    [InlineData(WorkflowLifecycleStatus.Archived, WorkflowLifecycleStatus.Draft, false)]
    [InlineData(WorkflowLifecycleStatus.Archived, WorkflowLifecycleStatus.Published, false)]
    public async Task CanTransitionAsync_DelegatesToWorkflowManager(
        WorkflowLifecycleStatus from, WorkflowLifecycleStatus to, bool expected)
    {
        WorkflowTemplateTransitionHook hook = CreateHook();

        bool result = await hook.CanTransitionAsync(from, to,
            TestContext.Current.CancellationToken);

        result.ShouldBe(expected);
    }

    // -------------------------------------------------------------------------
    // OnTransitionedAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OnTransitionedAsync_CompletesWithoutError()
    {
        WorkflowTemplateTransitionHook hook = CreateHook();

        await Should.NotThrowAsync(() => hook.OnTransitionedAsync(
            Guid.NewGuid(),
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published,
            "alice",
            TestContext.Current.CancellationToken));
    }
}
