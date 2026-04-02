using Granit.Authorization;
using Granit.Diagnostics.Endpoints.Internal;
using Granit.Localization;

namespace Granit.Diagnostics.Endpoints.Permissions;

/// <summary>
/// Declares permission definitions for diagnostics monitoring endpoints.
/// Auto-discovered by <c>GranitAuthorizationModule</c>.
/// </summary>
internal sealed class DiagnosticsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            DiagnosticsPermissions.GroupName,
            LocalizableString.Create<DiagnosticsEndpointsLocalizationResource>(
                "PermissionGroup:Diagnostics"));

        group.AddPermission(
            DiagnosticsPermissions.Monitoring.Read,
            LocalizableString.Create<DiagnosticsEndpointsLocalizationResource>(
                "Permission:Diagnostics.Monitoring.Read"));
    }
}
