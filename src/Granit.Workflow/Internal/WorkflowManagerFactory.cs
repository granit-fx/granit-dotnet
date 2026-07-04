using Granit.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Workflow.Internal;

/// <summary>
/// Default <see cref="IWorkflowManagerFactory"/> that resolves keyed
/// <see cref="IWorkflowManager{TState}"/> instances from the current scope.
/// </summary>
/// <remarks>
/// Registered as scoped so the injected <see cref="IServiceProvider"/> is the request scope:
/// the resolved managers and their scoped dependencies (<see cref="IWorkflowPermissionChecker"/>,
/// <c>ICurrentTenant</c>) stay within the caller's scope.
/// </remarks>
internal sealed class WorkflowManagerFactory(IServiceProvider serviceProvider) : IWorkflowManagerFactory
{
    /// <inheritdoc/>
    public IWorkflowManager<TState> GetManager<TState>(string workflowEntityType)
        where TState : struct, Enum
    {
        ArgumentException.ThrowIfNullOrEmpty(workflowEntityType);
        return serviceProvider.GetRequiredKeyedService<IWorkflowManager<TState>>(workflowEntityType);
    }

    /// <inheritdoc/>
    public IWorkflowManager<TState> GetManager<TEntity, TState>()
        where TEntity : IWorkflowStateful
        where TState : struct, Enum
        => GetManager<TState>(TEntity.WorkflowEntityType);
}
