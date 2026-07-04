using Microsoft.Extensions.DependencyInjection;

namespace Granit.Workflow.Scheduling.Internal;

/// <summary>
/// Resolves keyed <see cref="IWorkflowTransitionApplier"/> registrations from the current scope.
/// </summary>
/// <remarks>
/// Registered as scoped so the injected <see cref="IServiceProvider"/> is the scoped provider,
/// allowing scoped appliers (which typically own a request/handler-scoped DbContext) to resolve
/// correctly within the Wolverine handler scope.
/// </remarks>
internal sealed class WorkflowTransitionApplierRegistry(IServiceProvider serviceProvider)
    : IWorkflowTransitionApplierRegistry
{
    /// <inheritdoc/>
    public IWorkflowTransitionApplier Resolve(string workflowEntityType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowEntityType);

        IWorkflowTransitionApplier? applier =
            serviceProvider.GetKeyedService<IWorkflowTransitionApplier>(workflowEntityType);

        return applier ?? throw new InvalidOperationException(
            $"No {nameof(IWorkflowTransitionApplier)} is registered for workflow entity type '{workflowEntityType}'. "
            + $"Register one via services.AddWorkflowTransitionApplier<T>(\"{workflowEntityType}\").");
    }
}
