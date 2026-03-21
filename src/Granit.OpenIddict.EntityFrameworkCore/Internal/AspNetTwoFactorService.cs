using Granit.OpenIddict.Entities;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="ITwoFactorService"/> implementation backed by <see cref="UserManager{TUser}"/>.
/// </summary>
internal sealed class AspNetTwoFactorService(
    UserManager<GranitUser> userManager,
    ITotpService totpService) : ITwoFactorService
{
    /// <inheritdoc/>
    public async Task<TwoFactorStatus> GetStatusAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser user = await FindUserAsync(userId).ConfigureAwait(false);

        bool isEnabled = await userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false);
        string? key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        int recoveryCodesLeft = await userManager.CountRecoveryCodesAsync(user).ConfigureAwait(false);

        return new TwoFactorStatus(isEnabled, !string.IsNullOrEmpty(key), recoveryCodesLeft);
    }

    /// <inheritdoc/>
    public async Task<AuthenticatorKeyInfo> GetAuthenticatorKeyAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser user = await FindUserAsync(userId).ConfigureAwait(false);

        string? key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
            key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        }

        string email = user.Email ?? user.UserName ?? userId;
        string qrCodeUri = totpService.GetQrCodeUri(email, key!);

        return new AuthenticatorKeyInfo(key!, qrCodeUri);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> EnableAsync(
        string userId, string code, CancellationToken cancellationToken = default)
    {
        GranitUser user = await FindUserAsync(userId).ConfigureAwait(false);

        string? key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key) || !totpService.ValidateCode(key, code))
        {
            throw new InvalidOperationException("Invalid TOTP verification code.");
        }

        await userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);

        IEnumerable<string>? codes = await userManager
            .GenerateNewTwoFactorRecoveryCodesAsync(user, 10).ConfigureAwait(false);

        return codes?.ToList() ?? [];
    }

    /// <inheritdoc/>
    public async Task DisableAsync(string userId, CancellationToken cancellationToken = default)
    {
        GranitUser user = await FindUserAsync(userId).ConfigureAwait(false);
        await userManager.SetTwoFactorEnabledAsync(user, false).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ResetAuthenticatorAsync(string userId, CancellationToken cancellationToken = default)
    {
        GranitUser user = await FindUserAsync(userId).ConfigureAwait(false);
        await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser user = await FindUserAsync(userId).ConfigureAwait(false);
        IEnumerable<string>? codes = await userManager
            .GenerateNewTwoFactorRecoveryCodesAsync(user, 10).ConfigureAwait(false);
        return codes?.ToList() ?? [];
    }

    private async Task<GranitUser> FindUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"User {userId} not found.");
}
