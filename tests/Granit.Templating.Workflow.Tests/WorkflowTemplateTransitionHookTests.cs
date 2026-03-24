using Granit.MultiTenancy;
using Granit.Templating.Store;
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
        IWorkflowManager<WorkflowLifecycleStatus>? workflowManager = null,
        IWorkflowTransitionRecorder? recorder = null,
        ICurrentTenant? currentTenant = null)
    {
        IWorkflowManager<WorkflowLifecycleStatus> manager = workflowManager ?? CreateDefaultManager();
        IWorkflowTransitionRecorder rec = recorder ?? Substitute.For<IWorkflowTransitionRecorder>();
        ICurrentTenant tenant = currentTenant ?? CreateNullTenant();

        return new WorkflowTemplateTransitionHook(manager, rec, tenant);
    }

    private static IWorkflowManager<WorkflowLifecycleStatus> CreateDefaultManager()
    {
        IWorkflowManager<WorkflowLifecycleStatus> manager = Substitute.For<IWorkflowManager<WorkflowLifecycleStatus>>();
        manager.GetAllowedTransitionsAsync(Arg.Any<WorkflowLifecycleStatus>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                WorkflowLifecycleStatus from = callInfo.Arg<WorkflowLifecycleStatus>();
                List<WorkflowTransition<WorkflowLifecycleStatus>> transitions = from switch
                {
                    WorkflowLifecycleStatus.Draft =>
                    [
                        new() { From = from, To = WorkflowLifecycleStatus.Published },
                        new() { From = from, To = WorkflowLifecycleStatus.PendingReview },
                    ],
                    WorkflowLifecycleStatus.Published =>
                    [
                        new() { From = from, To = WorkflowLifecycleStatus.Archived },
                        new() { From = from, To = WorkflowLifecycleStatus.Draft },
                    ],
                    WorkflowLifecycleStatus.PendingReview =>
                    [
                        new() { From = from, To = WorkflowLifecycleStatus.Published },
                    ],
                    _ => [],
                };
                return (IReadOnlyList<WorkflowTransition<WorkflowLifecycleStatus>>)transitions;
            });
        return manager;
    }

    private static ICurrentTenant CreateNullTenant()
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        tenant.Id.Returns((Guid?)null);
        return tenant;
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
    [InlineData(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, true)]
    [InlineData(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.PendingReview, true)]
    [InlineData(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived, true)]
    [InlineData(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Draft, true)]
    [InlineData(TemplateLifecycleStatus.PendingReview, TemplateLifecycleStatus.Published, true)]
    [InlineData(TemplateLifecycleStatus.Archived, TemplateLifecycleStatus.Draft, false)]
    [InlineData(TemplateLifecycleStatus.Archived, TemplateLifecycleStatus.Published, false)]
    public async Task CanTransitionAsync_DelegatesToWorkflowManager(
        TemplateLifecycleStatus from, TemplateLifecycleStatus to, bool expected)
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
    public async Task OnTransitionedAsync_DelegatesToRecorder()
    {
        IWorkflowTransitionRecorder recorder = Substitute.For<IWorkflowTransitionRecorder>();
        WorkflowTemplateTransitionHook hook = CreateHook(recorder: recorder);
        var revisionId = Guid.NewGuid();

        await hook.OnTransitionedAsync(
            revisionId, TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, "alice",
            TestContext.Current.CancellationToken);

        await recorder.Received(1).RecordTransitionAsync(
            Arg.Is<RecordTransitionRequest>(r =>
                r.EntityType == "TemplateRevision" &&
                r.EntityId == revisionId.ToString() &&
                r.PreviousState == "Draft" &&
                r.NewState == "Published" &&
                r.UserId == "alice" &&
                r.Comment == null &&
                r.TenantId == null),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task OnTransitionedAsync_CapturesComment()
    {
        IWorkflowTransitionRecorder recorder = Substitute.For<IWorkflowTransitionRecorder>();
        WorkflowTemplateTransitionHook hook = CreateHook(recorder: recorder);

        using (WorkflowTransitionContext.SetComment("Validé par le directeur médical"))
        {
            await hook.OnTransitionedAsync(
                Guid.NewGuid(), TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, "alice",
                TestContext.Current.CancellationToken);
        }

        await recorder.Received(1).RecordTransitionAsync(
            Arg.Is<RecordTransitionRequest>(r =>
                r.Comment == "Validé par le directeur médical"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnTransitionedAsync_IncludesTenantId_WhenAvailable()
    {
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);

        IWorkflowTransitionRecorder recorder = Substitute.For<IWorkflowTransitionRecorder>();
        WorkflowTemplateTransitionHook hook = CreateHook(recorder: recorder, currentTenant: tenant);

        await hook.OnTransitionedAsync(
            Guid.NewGuid(), TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, "alice",
            TestContext.Current.CancellationToken);

        await recorder.Received(1).RecordTransitionAsync(
            Arg.Is<RecordTransitionRequest>(r =>
                r.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnTransitionedAsync_NullTenantId_WhenNotAvailable()
    {
        IWorkflowTransitionRecorder recorder = Substitute.For<IWorkflowTransitionRecorder>();
        WorkflowTemplateTransitionHook hook = CreateHook(recorder: recorder);

        await hook.OnTransitionedAsync(
            Guid.NewGuid(), TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, "alice",
            TestContext.Current.CancellationToken);

        await recorder.Received(1).RecordTransitionAsync(
            Arg.Is<RecordTransitionRequest>(r =>
                r.TenantId == null),
            Arg.Any<CancellationToken>());
    }
}
