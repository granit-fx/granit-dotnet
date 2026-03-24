namespace Granit.Templating.Store;

/// <summary>
/// Default no-op implementation of <see cref="ITemplateTransitionHook"/>.
/// Allows simple lifecycle transitions without any Workflow dependency.
/// </summary>
/// <remarks>
/// Same pattern as <c>NullTenantContext</c> in <c>Granit.MultiTenancy</c>:
/// provides a safe default when the Workflow module is not installed.
/// </remarks>
internal sealed class NullTemplateTransitionHook : ITemplateTransitionHook
{
    /// <inheritdoc/>
    public bool IsWorkflowEnabled => false;

    /// <inheritdoc/>
    public Task<bool> CanTransitionAsync(
        TemplateLifecycleStatus from,
        TemplateLifecycleStatus target,
        CancellationToken cancellationToken = default) =>
        Task.FromResult((from, target) is
            (TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published) or
            (TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived) or
            (TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Draft));

    /// <inheritdoc/>
    public Task OnTransitionedAsync(
        Guid revisionId,
        TemplateLifecycleStatus from,
        TemplateLifecycleStatus target,
        string userId,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
