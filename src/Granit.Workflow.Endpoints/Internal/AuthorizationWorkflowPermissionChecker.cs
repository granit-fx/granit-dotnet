using Granit.Authorization;

namespace Granit.Workflow.Endpoints.Internal;

/// <summary>
/// Bridge implementation of <see cref="IWorkflowPermissionChecker"/> that delegates
/// to the Granit RBAC <see cref="IPermissionChecker"/> pipeline.
/// Registered by <see cref="Extensions.WorkflowEndpointsServiceCollectionExtensions"/>
/// when <c>Granit.Authorization</c> is available, replacing the null-object default.
/// </summary>
internal sealed class AuthorizationWorkflowPermissionChecker(
    IPermissionChecker permissionChecker) : IWorkflowPermissionChecker
{
    /// <inheritdoc/>
    public Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default) =>
        permissionChecker.IsGrantedAsync(permissionName, cancellationToken);
}
