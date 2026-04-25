using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Models;

namespace Granit.Identity.Endpoints.Internal;

internal static class IdentityResponseMapper
{
    internal static IdentityUserResponse ToResponse(IIdentityUser user) =>
        new(user.UserId, user.Username, user.Email, user.FirstName, user.LastName, user.Enabled, user.Metadata);

    internal static IdentityRoleResponse ToResponse(IdentityRole role) =>
        new(role.Id, role.Name, role.Description);

    internal static IdentityGroupResponse ToResponse(IdentityGroup group) =>
        new(group.Id, group.Name, group.Path, group.SubGroups.Select(ToResponse).ToList());

    internal static IdentitySessionResponse ToResponse(IdentitySession session) =>
        new(session.SessionId, session.IpAddress, session.StartedAt, session.LastAccess, session.RememberMe, session.Clients);

    internal static IdentityDeviceActivityResponse ToResponse(IdentityDeviceActivity device) =>
        new(device.IpAddress, device.LastAccess, device.Device, device.Os, device.OsVersion, device.Browser, device.Mobile, device.Current, device.Sessions.Select(ToResponse).ToList());
}
