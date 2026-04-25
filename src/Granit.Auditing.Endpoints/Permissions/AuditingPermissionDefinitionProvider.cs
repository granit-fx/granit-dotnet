using Granit.Auditing.Endpoints.Internal;
using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;

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

        // Reading audit entries is meaningful in both scopes: a tenant admin reads the
        // tenant's entries; a host admin reads cross-tenant entries.
        group.AddPermission(
            AuditingPermissions.AuditEntries.Read,
            LocalizableString.Create<AuditingEndpointsLocalizationResource>(
                "Permission:Auditing.AuditEntries.Read"),
            MultiTenancySides.Both);

        // Managing (pruning, redacting) audit entries is an infrastructure concern and
        // should not be delegated to tenant admins — mis-use could break compliance trails.
        group.AddPermission(
            AuditingPermissions.AuditEntries.Manage,
            LocalizableString.Create<AuditingEndpointsLocalizationResource>(
                "Permission:Auditing.AuditEntries.Manage"),
            MultiTenancySides.Host);
    }
}
