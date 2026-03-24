using Granit.Authorization.Abstractions;
using Granit.Localization;
using Granit.Templating.Endpoints.Internal;

namespace Granit.Templating.Endpoints.Permissions;

/// <summary>
/// Declares the <c>Templating.Templates.Read</c> and <c>Templating.Templates.Manage</c> permissions in the Granit RBAC system.
/// </summary>
/// <remarks>
/// Registered automatically by <see cref="GranitTemplatingEndpointsModule"/>.
/// </remarks>
internal sealed class TemplatingPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    /// <inheritdoc />
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            TemplatingPermissions.GroupName,
            LocalizableString.Create<TemplatingEndpointsLocalizationResource>(
                "PermissionGroup:Templating"));

        group.AddPermission(
            TemplatingPermissions.Templates.Read,
            LocalizableString.Create<TemplatingEndpointsLocalizationResource>(
                "Permission:Templating.Templates.Read"));

        group.AddPermission(
            TemplatingPermissions.Templates.Manage,
            LocalizableString.Create<TemplatingEndpointsLocalizationResource>(
                "Permission:Templating.Templates.Manage"));

        group.AddPermission(
            TemplatingPermissions.Categories.Read,
            LocalizableString.Create<TemplatingEndpointsLocalizationResource>(
                "Permission:Templating.Categories.Read"));

        group.AddPermission(
            TemplatingPermissions.Categories.Manage,
            LocalizableString.Create<TemplatingEndpointsLocalizationResource>(
                "Permission:Templating.Categories.Manage"));
    }
}
