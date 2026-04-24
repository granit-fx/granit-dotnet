using Granit.Authorization;
using Granit.Localization;
using Granit.Metering.Endpoints.Internal;
using Granit.MultiTenancy;

namespace Granit.Metering.Endpoints.Permissions;

internal sealed class MeteringPermissionDefinitionProvider : IPermissionDefinitionProvider
{
    public void DefinePermissions(IPermissionDefinitionContext context)
    {
        PermissionGroup group = context.AddGroup(
            MeteringPermissions.GroupName,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>(
                "PermissionGroup:Metering"));

        group.AddPermission(MeteringPermissions.Meters.Read,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Meters.Read"),
            MultiTenancySide.Both);
        group.AddPermission(MeteringPermissions.Meters.Manage,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Meters.Manage"),
            MultiTenancySide.Both);
        group.AddPermission(MeteringPermissions.Usage.Read,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Usage.Read"),
            MultiTenancySide.Both);
        group.AddPermission(MeteringPermissions.Usage.Record,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Usage.Record"),
            MultiTenancySide.Both);
        group.AddPermission(MeteringPermissions.Events.Backfill,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Events.Backfill"),
            MultiTenancySide.Both);
    }
}
