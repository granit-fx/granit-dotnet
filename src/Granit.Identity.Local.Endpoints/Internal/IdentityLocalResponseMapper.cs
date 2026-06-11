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

    internal static ExternalLoginCallbackResponse ToResponse(ProcessCallbackResult result, string? continuationToken) =>
        result.Status == ProcessCallbackStatus.NewUserNeedsProfile
            ? new ExternalLoginCallbackResponse(
                ExternalLoginCallbackResponse.StatusNeedsProfileCompletion,
                UserId: null,
                IsNewUser: false,
                ContinuationToken: continuationToken,
                Prefill: new ExternalProfilePrefillResponse(
                    result.Prefill!.Email, result.Prefill.FirstName, result.Prefill.LastName))
            : new ExternalLoginCallbackResponse(
                ExternalLoginCallbackResponse.StatusCompleted,
                result.UserId,
                result.IsNewUser,
                ContinuationToken: null,
                Prefill: null);
}
