using System.Diagnostics.Metrics;
using Granit.Core.MultiTenancy;
using Granit.Workflow.Diagnostics;
using Granit.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

public sealed class WorkflowManagerTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly WorkflowMetrics _metrics;
    private static readonly WorkflowDefinition<WorkflowLifecycleStatus> Definition =
        WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
            .InitialState(WorkflowLifecycleStatus.Draft)
            .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.PendingReview, t => t
                .Named("Soumettre")
                .RequiresPermission("workflow.submit"))
            .Transition(WorkflowLifecycleStatus.PendingReview, WorkflowLifecycleStatus.Published, t => t
                .Named("Publier")
                .RequiresPermission("workflow.publish")
                .RequiresApproval())
            .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, t => t
                .Named("Publication directe")
                .RequiresPermission("workflow.publish")
                .RequiresApproval())
            .Transition(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived, t => t
                .Named("Archiver")
                .RequiresPermission("workflow.archive")));

    private readonly IWorkflowPermissionChecker _permissionChecker = Substitute.For<IWorkflowPermissionChecker>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public WorkflowManagerTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new WorkflowMetrics(meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    // ========================================================================
    // TransitionAsync — valid transitions
    // ========================================================================

    [Fact]
    public async Task TransitionAsync_WithPermission_ShouldComplete()
    {
        // Arrange
        _permissionChecker.IsGrantedAsync("workflow.submit", Arg.Any<CancellationToken>())
            .Returns(true);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.PendingReview,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.ResultingState.ShouldBe(WorkflowLifecycleStatus.PendingReview);
        result.Outcome.ShouldBe(TransitionOutcome.Completed);
    }

    [Fact]
    public async Task TransitionAsync_WithNoRequiredPermission_ShouldComplete()
    {
        // Arrange — create a definition with no permission required
        var openDefinition =
            WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
                .InitialState(WorkflowLifecycleStatus.Draft)
                .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published));
        WorkflowManager<WorkflowLifecycleStatus> manager = new(openDefinition, _permissionChecker, _metrics, _currentTenant);

        // Act
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.Outcome.ShouldBe(TransitionOutcome.Completed);
    }

    // ========================================================================
    // TransitionAsync — invalid transitions
    // ========================================================================

    [Fact]
    public async Task TransitionAsync_InvalidTransition_ShouldReturnInvalidTransition()
    {
        // Arrange
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act — Draft → Archived is not defined
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Archived,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ResultingState.ShouldBe(WorkflowLifecycleStatus.Draft);
        result.Outcome.ShouldBe(TransitionOutcome.InvalidTransition);
    }

    // ========================================================================
    // TransitionAsync — denied (no permission, no approval path)
    // ========================================================================

    [Fact]
    public async Task TransitionAsync_WithoutPermission_NoApproval_ShouldDeny()
    {
        // Arrange — workflow.submit does not have RequiresApproval
        _permissionChecker.IsGrantedAsync("workflow.submit", Arg.Any<CancellationToken>())
            .Returns(false);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.PendingReview,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ResultingState.ShouldBe(WorkflowLifecycleStatus.Draft);
        result.Outcome.ShouldBe(TransitionOutcome.Denied);
    }

    // ========================================================================
    // TransitionAsync — approval routing
    // ========================================================================

    [Fact]
    public async Task TransitionAsync_WithoutPermission_WithApproval_ShouldRouteToApproval()
    {
        // Arrange — workflow.publish has RequiresApproval
        _permissionChecker.IsGrantedAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns(false);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.ResultingState.ShouldBe(WorkflowLifecycleStatus.PendingReview);
        result.Outcome.ShouldBe(TransitionOutcome.ApprovalRequested);
    }

    [Fact]
    public async Task TransitionAsync_WithPermission_RequiresApproval_ShouldComplete()
    {
        // Arrange — user has the permission, so approval routing is bypassed
        _permissionChecker.IsGrantedAsync("workflow.publish", Arg.Any<CancellationToken>())
            .Returns(true);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.Published,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        result.ResultingState.ShouldBe(WorkflowLifecycleStatus.Published);
        result.Outcome.ShouldBe(TransitionOutcome.Completed);
    }

    // ========================================================================
    // TransitionAsync — comment context
    // ========================================================================

    [Fact]
    public async Task TransitionAsync_WithComment_ShouldSetAsyncLocalContext()
    {
        // Arrange
        _permissionChecker.IsGrantedAsync("workflow.submit", Arg.Any<CancellationToken>())
            .Returns(true);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();
        TransitionContext context = new() { Comment = "Validated by Dr. Martin" };

        // Act
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.PendingReview,
            context,
            TestContext.Current.CancellationToken);

        // Assert
        result.Succeeded.ShouldBeTrue();
        WorkflowTransitionContext.Current?.Comment.ShouldBe("Validated by Dr. Martin");
    }

    // ========================================================================
    // GetAllowedTransitionsAsync
    // ========================================================================

    [Fact]
    public async Task GetAllowedTransitionsAsync_UserWithPermission_ShouldIncludeAll()
    {
        // Arrange
        _permissionChecker.IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act
        IReadOnlyList<WorkflowTransition<WorkflowLifecycleStatus>> transitions =
            await manager.GetAllowedTransitionsAsync(
                WorkflowLifecycleStatus.Draft,
                TestContext.Current.CancellationToken);

        // Assert — Draft has 2 transitions: PendingReview and Published
        transitions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetAllowedTransitionsAsync_UserWithoutPermission_ShouldIncludeApprovalOnly()
    {
        // Arrange — user has no permissions
        _permissionChecker.IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        WorkflowManager<WorkflowLifecycleStatus> manager = BuildManager();

        // Act
        IReadOnlyList<WorkflowTransition<WorkflowLifecycleStatus>> transitions =
            await manager.GetAllowedTransitionsAsync(
                WorkflowLifecycleStatus.Draft,
                TestContext.Current.CancellationToken);

        // Assert — only "Publication directe" (RequiresApproval=true) should be available
        transitions.Count.ShouldBe(1);
        transitions[0].RequiresApproval.ShouldBeTrue();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private WorkflowManager<WorkflowLifecycleStatus> BuildManager() =>
        new(Definition, _permissionChecker, _metrics, _currentTenant);
}
