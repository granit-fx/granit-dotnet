using Granit.Authorization;
using Granit.Localization;
using Granit.Metering.Endpoints.Internal;

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
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Meters.Read"));
        group.AddPermission(MeteringPermissions.Meters.Manage,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Meters.Manage"));
        group.AddPermission(MeteringPermissions.Usage.Read,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Usage.Read"));
        group.AddPermission(MeteringPermissions.Usage.Record,
            LocalizableString.Create<MeteringEndpointsLocalizationResource>("Permission:Metering.Usage.Record"));
    }
}
