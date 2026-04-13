using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Services;

namespace Granit.Identity.Local.Endpoints.Internal;

internal static class IdentityLocalResponseMapper
{
    internal static PasskeyInfoResponse ToResponse(PasskeyInfo info) =>
        new(info.Id, info.Name, info.CreatedAt, info.LastUsedAt);

    internal static ExternalLoginInfoResponse ToResponse(ExternalLoginInfo info) =>
        new(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName);

    internal static ImpersonationResponse ToResponse(ImpersonationResult result) =>
        new(result.AccessToken, result.RefreshToken, result.ExpiresIn);
}
