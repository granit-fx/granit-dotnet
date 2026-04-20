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

        // Plans and Prices are the SaaS catalog: they are authored at the host level only.
        group.AddPermission(SubscriptionsPermissions.Plans.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Plans.Read"),
            MultiTenancySide.Host);
        group.AddPermission(SubscriptionsPermissions.Plans.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Plans.Manage"),
            MultiTenancySide.Host);
        group.AddPermission(SubscriptionsPermissions.Prices.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Prices.Read"),
            MultiTenancySide.Host);
        group.AddPermission(SubscriptionsPermissions.Prices.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Prices.Manage"),
            MultiTenancySide.Host);

        // Subscriptions and Seats: the host provisions and adjusts tenant subscriptions,
        // and each tenant also needs to see/manage its own plan and seats. Both-sided.
        group.AddPermission(SubscriptionsPermissions.Subscriptions.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Subscriptions.Read"),
            MultiTenancySide.Both);
        group.AddPermission(SubscriptionsPermissions.Subscriptions.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Subscriptions.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(SubscriptionsPermissions.Seats.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Seats.Read"),
            MultiTenancySide.Both);
        group.AddPermission(SubscriptionsPermissions.Seats.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Seats.Manage"),
            MultiTenancySide.Both);
    }
}
