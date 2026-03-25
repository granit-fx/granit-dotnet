using Granit.Auditing.Endpoints.Internal;
using Granit.Authorization.Abstractions;
using Granit.Localization;

namespace Granit.Auditing.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for audit log endpoints.
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class AuditingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AuditingPermissions.GroupName,
            LocalizableString.Create<AuditingEndpointsLocalizationResource>(
                "PermissionGroup:Auditing"));

        group.AddPermission(
            AuditingPermissions.AuditEntries.Read,
            LocalizableString.Create<AuditingEndpointsLocalizationResource>(
                "Permission:Auditing.AuditEntries.Read"));
    }
}
