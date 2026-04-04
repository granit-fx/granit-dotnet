using Granit.Authorization;
using Granit.Localization;
using Granit.Subscriptions.Endpoints.Internal;

namespace Granit.Subscriptions.Endpoints.Permissions;

internal sealed class SubscriptionsPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            SubscriptionsPermissions.GroupName,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>(
                "PermissionGroup:Subscriptions"));

        group.AddPermission(SubscriptionsPermissions.Plans.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Plans.Read"));
        group.AddPermission(SubscriptionsPermissions.Plans.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Plans.Manage"));
        group.AddPermission(SubscriptionsPermissions.Subscriptions.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Subscriptions.Read"));
        group.AddPermission(SubscriptionsPermissions.Subscriptions.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Subscriptions.Manage"));
        group.AddPermission(SubscriptionsPermissions.Prices.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Prices.Read"));
        group.AddPermission(SubscriptionsPermissions.Prices.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Prices.Manage"));
        group.AddPermission(SubscriptionsPermissions.Seats.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Seats.Read"));
        group.AddPermission(SubscriptionsPermissions.Seats.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Seats.Manage"));
    }
}
