using System.Diagnostics.Metrics;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Workflow.Diagnostics;
using Granit.Workflow.Domain;
using Granit.Workflow.Events;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// End-to-end test demonstrating the caller-side pattern required to raise
/// <see cref="WorkflowApprovalRequestedEvent"/> when <see cref="WorkflowManager{TState}.TransitionAsync"/>
/// returns <see cref="TransitionOutcome.ApprovalRequested"/>. This context (target state + required
/// permission) does not survive to persistence time, so the framework cannot auto-raise this event
/// the way <c>WorkflowTransitionInterceptor</c> does for <see cref="WorkflowStateChangedEvent"/>.
/// See the <c>&lt;example&gt;</c> on <see cref="WorkflowManager{TState}.TransitionAsync"/>.
/// </summary>
public sealed class WorkflowApprovalCallerPatternTests : IDisposable
{
    private const string PublishPermission = "workflow.publish";

    private static readonly WorkflowDefinition<WorkflowLifecycleStatus> Definition =
        WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
            .InitialState(WorkflowLifecycleStatus.Draft)
            .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, t => t
                .Named("Publier")
                .RequiresPermission(PublishPermission)
                .RequiresApproval()));

    private readonly ServiceProvider _sp;
    private readonly WorkflowMetrics _metrics;
    private readonly IWorkflowPermissionChecker _permissionChecker = Substitute.For<IWorkflowPermissionChecker>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public WorkflowApprovalCallerPatternTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<IMeterFactory>();
        _metrics = new WorkflowMetrics(meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public async Task TransitionAsync_WhenApprovalRequested_CallerMustManuallyRaiseApprovalEvent()
    {
        // Arrange — user lacks the required permission, but the transition allows approval routing.
        _permissionChecker.IsGrantedAsync(PublishPermission, Arg.Any<CancellationToken>())
            .Returns(false);

        WorkflowManager<WorkflowLifecycleStatus> manager = new(Definition, _permissionChecker, _metrics, _currentTenant);
        var invoice = TestInvoice.Create(Guid.NewGuid());

        // Act — 1) the manager only computes the outcome, it never touches the entity.
        TransitionResult<WorkflowLifecycleStatus> result = await manager.TransitionAsync(
            invoice.Status,
            WorkflowLifecycleStatus.Published,
            cancellationToken: TestContext.Current.CancellationToken);

        result.Outcome.ShouldBe(TransitionOutcome.ApprovalRequested);

        // Act — 2) caller-side pattern required by WorkflowManager<TState>.TransitionAsync's
        // documented <example>: set the persisted state, then manually raise the approval event
        // with the call-site-only context (original target state + denied permission).
        invoice.SetStatus(result.ResultingState);
        invoice.RaiseApprovalRequested("user-7", targetState: "Published", requiredPermission: PublishPermission);

        // Assert
        invoice.Status.ShouldBe(result.ResultingState);
        invoice.DomainEvents.ShouldHaveSingleItem();
        WorkflowApprovalRequestedEvent evt = invoice.DomainEvents.OfType<WorkflowApprovalRequestedEvent>().Single();
        evt.EntityType.ShouldBe(nameof(TestInvoice));
        evt.EntityId.ShouldBe(invoice.Id.ToString());
        evt.RequestedBy.ShouldBe("user-7");
        evt.TargetState.ShouldBe("Published");
        evt.RequiredPermission.ShouldBe(PublishPermission);
    }

    /// <summary>Minimal aggregate root standing in for a real entity implementing <see cref="IWorkflowStateful"/>.</summary>
    private sealed class TestInvoice : AuditedAggregateRoot, IWorkflowStateful
    {
        private TestInvoice()
        {
        }

        public WorkflowLifecycleStatus Status { get; private set; }

        public static string StatusPropertyName => nameof(Status);

        public static string WorkflowEntityType => nameof(TestInvoice);

        public static TestInvoice Create(Guid id) => new() { Id = id, Status = WorkflowLifecycleStatus.Draft };

        public string GetWorkflowEntityId() => Id.ToString();

        public void RaiseWorkflowStateChangedEvent(string entityType, string previousState, string newState, string transitionedBy) =>
            AddDomainEvent(new WorkflowStateChangedEvent(entityType, GetWorkflowEntityId(), previousState, newState, transitionedBy));

        public void SetStatus(WorkflowLifecycleStatus status) => Status = status;

        public void RaiseApprovalRequested(string requestedBy, string targetState, string requiredPermission) =>
            AddDomainEvent(new WorkflowApprovalRequestedEvent(
                WorkflowEntityType, GetWorkflowEntityId(), requestedBy, targetState, requiredPermission));
    }
}
