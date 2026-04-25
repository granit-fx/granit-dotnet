using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Workflow.Endpoints.Internal;

namespace Granit.Workflow.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Workflow.History.Read</c> permission in the Granit RBAC system.
/// </summary>
/// <remarks>
/// Registered automatically by <see cref="GranitWorkflowEndpointsModule"/>.
/// Once registered, <c>DynamicPermissionPolicyProvider</c> creates the authorization policy
/// via <c>PermissionRequirement</c> — the full <c>IPermissionChecker</c> pipeline is used.
/// </remarks>
internal sealed class WorkflowPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            WorkflowPermissions.GroupName,
            LocalizableString.Create<WorkflowEndpointsLocalizationResource>(
                "PermissionGroup:Workflow"));

        group.AddPermission(
            WorkflowPermissions.History.Read,
            LocalizableString.Create<WorkflowEndpointsLocalizationResource>(
                "Permission:Workflow.History.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            WorkflowPermissions.Transitions.Read,
            LocalizableString.Create<WorkflowEndpointsLocalizationResource>(
                "Permission:Workflow.Transitions.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            WorkflowPermissions.Transitions.Execute,
            LocalizableString.Create<WorkflowEndpointsLocalizationResource>(
                "Permission:Workflow.Transitions.Execute"),
            MultiTenancySides.Both);
    }
}
