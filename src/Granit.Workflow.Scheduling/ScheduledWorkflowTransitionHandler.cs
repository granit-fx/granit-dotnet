using Microsoft.Extensions.Logging;

namespace Granit.Workflow.Scheduling;

/// <summary>
/// Wolverine handler for <see cref="ScheduledWorkflowTransitionPayload"/>. Resolves the
/// entity-specific <see cref="IWorkflowTransitionApplier"/> by
/// <see cref="ScheduledWorkflowTransitionPayload.WorkflowEntityType"/> and applies the transition.
/// </summary>
/// <remarks>
/// Atomic single-execution (Pending → Processing), retry and terminal status transitions are
/// handled by <c>Granit.Scheduling.Wolverine</c>'s <c>ScheduledActionStatusMiddleware</c>, which
/// wraps every <c>IScheduledPayload</c> handler — so this handler only needs to dispatch.
/// The type is a non-static <c>public class</c> with a <c>public static</c> handle method per the
/// Granit Wolverine discovery contract.
/// </remarks>
public partial class ScheduledWorkflowTransitionHandler
{
    /// <summary>Applies the scheduled transition described by <paramref name="payload"/>.</summary>
    /// <param name="payload">The scheduled transition payload.</param>
    /// <param name="registry">The applier registry.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task Handle(
        ScheduledWorkflowTransitionPayload payload,
        IWorkflowTransitionApplierRegistry registry,
        ILogger<ScheduledWorkflowTransitionHandler> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(registry);

        Log.ApplyingTransition(logger, payload.WorkflowEntityType, payload.EntityId, payload.TargetState);

        IWorkflowTransitionApplier applier = registry.Resolve(payload.WorkflowEntityType);
        return applier.ApplyAsync(payload.EntityId, payload.TargetState, cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Applying scheduled workflow transition for {WorkflowEntityType} {EntityId} to state {TargetState}")]
        public static partial void ApplyingTransition(
            ILogger logger, string workflowEntityType, Guid entityId, string targetState);
    }
}
