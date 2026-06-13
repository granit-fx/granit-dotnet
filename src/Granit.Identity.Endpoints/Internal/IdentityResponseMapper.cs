using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Models;
using Granit.IpGeolocation;

namespace Granit.Identity.Endpoints.Internal;

internal static class IdentityResponseMapper
{
    internal static IdentityUserResponse ToResponse(IIdentityUser user) =>
        new(user.UserId, user.Username, user.Email, user.FirstName, user.LastName, user.Enabled, user.Metadata);

    internal static IdentityRoleResponse ToResponse(IdentityRole role) =>
        new(role.Id, role.Name, role.Description);

    internal static IdentityGroupResponse ToResponse(IdentityGroup group) =>
        new(group.Id, group.Name, group.Path, group.SubGroups.Select(ToResponse).ToList());

    internal static UserSessionResponse ToResponse(UserSessionView view, bool exposeRawIp) =>
        new(view.Session.SessionId, view.Session.IsCurrent, view.Session.CreatedAt, view.Session.LastAccessedAt,
            view.Session.UserAgent,
            exposeRawIp ? view.Session.IpAddress : IpMasking.Mask(view.Session.IpAddress),
            view.Session.Location, view.Risk?.Level, view.Risk?.Reasons);

    internal static UserDeviceResponse ToResponse(UserDevice device) =>
        new(device.DeviceId, device.Kind, device.OperatingSystem, device.Browser,
            device.LastSeen, device.SessionCount, device.LastLocation, device.IsTrusted, device.TrustedUntil);
}
