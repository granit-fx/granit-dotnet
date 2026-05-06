using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
using Granit.Taxonomy.Endpoints.Internal;

namespace Granit.Taxonomy.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Taxonomy.*.*</c> permissions in the Granit RBAC system.
/// Auto-discovered by <c>GranitAuthorizationModule</c> — no manual registration needed.
/// </summary>
internal sealed class TaxonomyPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PermissionGroup group = context.AddGroup(
            TaxonomyPermissions.GroupName,
            LocalizableString.Create<TaxonomyEndpointsLocalizationResource>(
                "PermissionGroup:Taxonomy"));

        group.AddPermission(
            TaxonomyPermissions.Tags.Read,
            LocalizableString.Create<TaxonomyEndpointsLocalizationResource>(
                "Permission:Taxonomy.Tags.Read"),
            MultiTenancySides.Both);

        group.AddPermission(
            TaxonomyPermissions.Tags.Manage,
            LocalizableString.Create<TaxonomyEndpointsLocalizationResource>(
                "Permission:Taxonomy.Tags.Manage"),
            MultiTenancySides.Both);
    }
}
