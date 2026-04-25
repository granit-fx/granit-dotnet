using Granit.Authorization;
using Granit.Localization;
using Granit.MultiTenancy;
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
            MultiTenancySides.Host);
        group.AddPermission(SubscriptionsPermissions.Plans.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Plans.Manage"),
            MultiTenancySides.Host);
        group.AddPermission(SubscriptionsPermissions.Prices.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Prices.Read"),
            MultiTenancySides.Host);
        group.AddPermission(SubscriptionsPermissions.Prices.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Prices.Manage"),
            MultiTenancySides.Host);

        // Subscriptions and Seats: the host provisions and adjusts tenant subscriptions,
        // and each tenant also needs to see/manage its own plan and seats. Both-sided.
        group.AddPermission(SubscriptionsPermissions.Subscriptions.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Subscriptions.Read"),
            MultiTenancySides.Both);
        group.AddPermission(SubscriptionsPermissions.Subscriptions.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Subscriptions.Manage"),
            MultiTenancySides.Both);
        group.AddPermission(SubscriptionsPermissions.Seats.Read,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Seats.Read"),
            MultiTenancySides.Both);
        group.AddPermission(SubscriptionsPermissions.Seats.Manage,
            LocalizableString.Create<SubscriptionsEndpointsLocalizationResource>("Permission:Subscriptions.Seats.Manage"),
            MultiTenancySides.Both);
    }
}
