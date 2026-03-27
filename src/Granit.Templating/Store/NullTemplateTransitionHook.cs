using Microsoft.Extensions.Logging;

namespace Granit.Templating.Store;

/// <summary>
/// Default no-op implementation of <see cref="ITemplateTransitionHook"/>.
/// Allows simple lifecycle transitions without any Workflow dependency.
/// </summary>
/// <remarks>
/// Same pattern as <c>NullTenantContext</c> in <c>Granit.MultiTenancy</c>:
/// provides a safe default when the Workflow module is not installed.
/// <para>
/// <strong>Security note:</strong> this implementation performs NO permission checks
/// on transitions. Add <c>Granit.Templating.Workflow</c> to enforce workflow approval
/// before publication (ISO 27001 compliance).
/// </para>
/// </remarks>
internal sealed partial class NullTemplateTransitionHook(
    ILogger<NullTemplateTransitionHook> logger) : ITemplateTransitionHook
{
    private int _warningLogged;

    /// <inheritdoc/>
    public bool IsWorkflowEnabled => false;

    /// <inheritdoc/>
    public Task<bool> CanTransitionAsync(
        TemplateLifecycleStatus from,
        TemplateLifecycleStatus target,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _warningLogged, 1, 0) == 0)
        {
            LogNoWorkflowModule();
        }

        return Task.FromResult((from, target) is
            (TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published) or
            (TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived) or
            (TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Draft));
    }

    /// <inheritdoc/>
    public Task OnTransitionedAsync(
        Guid revisionId,
        TemplateLifecycleStatus from,
        TemplateLifecycleStatus target,
        string userId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Granit.Templating.Workflow is not installed — template transitions bypass workflow approval. " +
                  "Add Granit.Templating.Workflow to enforce approval workflows (ISO 27001 compliance)")]
    private partial void LogNoWorkflowModule();
}
