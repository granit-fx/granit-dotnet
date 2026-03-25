using Granit.AuditLog.Endpoints.Internal;
using Granit.Authorization.Abstractions;
using Granit.Localization;

namespace Granit.AuditLog.Endpoints.Permissions;

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
            LocalizableString.Create<AuditLogEndpointsLocalizationResource>(
                "PermissionGroup:AuditLog"));

        group.AddPermission(
            AuditLogPermissions.Entries.Read,
            LocalizableString.Create<AuditLogEndpointsLocalizationResource>(
                "Permission:AuditLog.Entries.Read"));
    }
}
