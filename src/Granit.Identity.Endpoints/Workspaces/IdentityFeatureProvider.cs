using Granit.Identity.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Identity.Endpoints.Workspaces;

/// <summary>
/// Declares the identity module's features for the host's workspace
/// composition (per ADR-057). Four features: users, roles, groups, sessions —
/// each gated by its existing <see cref="IdentityPermissions"/> entry.
/// </summary>
/// <remarks>
/// Ships alongside the legacy <see cref="IdentityWorkspaceContribution"/>
/// during the phase 3 migration window. Default placement on the
/// <c>Granit.Framework.IdentityAccess</c> shell is provided by the
/// framework Defaults bundle (phase 2); the legacy contribution is
/// removed in phase 5.
/// </remarks>
internal sealed class IdentityFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(IdentityFeatures.Users, f => f
            .Permission(IdentityPermissions.Users.Read)
            .RouteName(IdentityFeatures.Users)
            .DefaultIcon("users-round")
            .DisplayKey("IdentityEndpoints:Workspace.Users"));

        catalog.Add(IdentityFeatures.Roles, f => f
            .Permission(IdentityPermissions.Roles.Read)
            .RouteName(IdentityFeatures.Roles)
            .DefaultIcon("shield-user")
            .DisplayKey("IdentityEndpoints:Workspace.Roles"));

        catalog.Add(IdentityFeatures.Groups, f => f
            .Permission(IdentityPermissions.Groups.Read)
            .RouteName(IdentityFeatures.Groups)
            .DefaultIcon("users")
            .DisplayKey("IdentityEndpoints:Workspace.Groups"));

        catalog.Add(IdentityFeatures.Sessions, f => f
            .Permission(IdentityPermissions.Sessions.Read)
            .RouteName(IdentityFeatures.Sessions)
            .DefaultIcon("monitor-smartphone")
            .DisplayKey("IdentityEndpoints:Workspace.Sessions"));
    }
}
