using Granit.Workflow.Domain;
using Microsoft.Extensions.Logging;

namespace Granit.Templating.Store;

/// <summary>
/// Default no-op implementation of <see cref="ITemplateTransitionHook"/>.
/// </summary>
internal sealed partial class NullTemplateTransitionHook(
    ILogger<NullTemplateTransitionHook> logger) : ITemplateTransitionHook
{
    private int _warningLogged;

    /// <inheritdoc/>
    public bool IsWorkflowEnabled => false;

    /// <inheritdoc/>
    public Task<bool> CanTransitionAsync(
        WorkflowLifecycleStatus from,
        WorkflowLifecycleStatus target,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _warningLogged, 1, 0) == 0)
        {
            LogNoWorkflowModule();
        }

        return Task.FromResult((from, target) is
            (WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published) or
            (WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived) or
            (WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Draft));
    }

    /// <inheritdoc/>
    public Task OnTransitionedAsync(
        Guid revisionId,
        WorkflowLifecycleStatus from,
        WorkflowLifecycleStatus target,
        string userId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit.Templating.Workflow is not installed -- template transitions bypass workflow approval. " +
                  "Add Granit.Templating.Workflow to enforce approval workflows (ISO 27001 compliance)")]
    private partial void LogNoWorkflowModule();
}
