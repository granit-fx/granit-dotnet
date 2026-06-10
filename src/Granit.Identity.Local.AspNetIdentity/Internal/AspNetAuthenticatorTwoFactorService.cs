using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IAuthenticatorTwoFactorService"/> implementation backed by
/// <see cref="UserManager{TUser}"/> and <see cref="ITotpService"/>.
/// </summary>
internal sealed class AspNetAuthenticatorTwoFactorService(
    UserManager<LocalIdentity> userManager,
    ITotpService totpService) : IAuthenticatorTwoFactorService
{
    /// <inheritdoc/>
    public async Task<AuthenticatorKeyInfo> GetKeyAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

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
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

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
    public async Task ResetAsync(string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);
        await userManager.ResetAuthenticatorKeyAsync(user).ConfigureAwait(false);
    }

    private async Task<LocalIdentity> FindUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"User {userId} not found.");
}
