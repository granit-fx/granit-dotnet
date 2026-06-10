using System.Security.Claims;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="ITwoFactorService"/> coordinator implementation backed by
/// <see cref="UserManager{TUser}"/>. Owns the factor-agnostic concerns; per-factor
/// enrollment lives in <see cref="AspNetAuthenticatorTwoFactorService"/> and
/// <see cref="AspNetEmailTwoFactorService"/>.
/// </summary>
internal sealed class AspNetTwoFactorService(
    UserManager<LocalIdentity> userManager) : ITwoFactorService
{
    /// <inheritdoc/>
    public async Task<TwoFactorStatus> GetStatusAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

        bool isEnabled = await userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false);
        string? key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        bool hasEmailOtp = await HasEmailOtpClaimAsync(user).ConfigureAwait(false);
        int recoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user).ConfigureAwait(false);

        return new TwoFactorStatus(isEnabled, !string.IsNullOrEmpty(key), hasEmailOtp, recoveryCodesLeft);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TwoFactorMethod>> GetAvailableMethodsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

        if (!await userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false))
        {
            return [];
        }

        var methods = new List<TwoFactorMethod>();

        string? key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(key))
        {
            methods.Add(TwoFactorMethod.Authenticator);
        }

        if (await HasEmailOtpClaimAsync(user).ConfigureAwait(false))
        {
            methods.Add(TwoFactorMethod.Email);
        }

        if (await userManager.CountRecoveryCodesAsync(user).ConfigureAwait(false) > 0)
        {
            methods.Add(TwoFactorMethod.RecoveryCode);
        }

        return methods;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);
        IEnumerable<string>? codes = await userManager
            .GenerateNewTwoFactorRecoveryCodesAsync(user, 10).ConfigureAwait(false);
        return codes?.ToList() ?? [];
    }

    /// <inheritdoc/>
    public async Task DisableAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

        await userManager.SetTwoFactorEnabledAsync(user, false).ConfigureAwait(false);
        await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);

        if (await HasEmailOtpClaimAsync(user).ConfigureAwait(false))
        {
            await userManager.RemoveClaimAsync(
                user, new Claim(TwoFactorClaims.EmailOtpEnabled, TwoFactorClaims.EnabledValue))
                .ConfigureAwait(false);
        }

        // Invalidate the security stamp to force re-authentication on existing sessions.
        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
    }

    private async Task<bool> HasEmailOtpClaimAsync(LocalIdentity user) =>
        (await userManager.GetClaimsAsync(user).ConfigureAwait(false))
            .Any(c => c.Type == TwoFactorClaims.EmailOtpEnabled && c.Value == TwoFactorClaims.EnabledValue);

    private async Task<LocalIdentity> FindUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"User {userId} not found.");
}
