using Granit.Auditing.Endpoints.Internal;
using Granit.Authorization.Abstractions;
using Granit.Localization;

namespace Granit.Auditing.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for audit log endpoints.
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class AuditLogPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            AuditLogPermissions.GroupName,
            LocalizableString.Create<AuditingEndpointsLocalizationResource>(
                "PermissionGroup:AuditLog"));

        group.AddPermission(
            AuditLogPermissions.Entries.Read,
            LocalizableString.Create<AuditingEndpointsLocalizationResource>(
                "Permission:AuditLog.Entries.Read"));
    }
}
