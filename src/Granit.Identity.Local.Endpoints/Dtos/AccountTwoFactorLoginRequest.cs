using Granit.Identity.Local.Services;

namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Request body for completing two-factor authentication during login.
/// </summary>
/// <param name="Code">The verification code: a TOTP code, an emailed one-time code, or a recovery code.</param>
/// <param name="Method">
/// Which factor <paramref name="Code"/> belongs to. Default: <see cref="TwoFactorMethod.Authenticator"/>.
/// </param>
/// <param name="RememberMe">
/// When <see langword="true"/>, the Identity session cookie is marked as persistent.
/// Default: <see langword="false"/>.
/// </param>
public sealed record AccountTwoFactorLoginRequest(
    string Code,
    TwoFactorMethod Method = TwoFactorMethod.Authenticator,
    bool RememberMe = false);
