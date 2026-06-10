using System.Security.Claims;
using Granit.Events;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IEmailTwoFactorService"/> implementation backed by ASP.NET Core Identity's
/// email token provider (<see cref="TokenOptions.DefaultEmailProvider"/>). Generates the
/// one-time code and publishes <see cref="TwoFactorEmailOtpRequestedEto"/> for delivery;
/// the code never leaves the service except over that event.
/// </summary>
#pragma warning disable GRSEC003 // One-time email code handling, not stored secrets
internal sealed class AspNetEmailTwoFactorService(
    UserManager<LocalIdentity> userManager,
    IDistributedEventBus eventBus) : IEmailTwoFactorService
{
    /// <inheritdoc/>
    public async Task SendCodeAsync(string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        string code = await userManager
            .GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new TwoFactorEmailOtpRequestedEto(user.Id, code, user.TenantId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task EnableAsync(string userId, string code, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

        bool valid = await userManager
            .VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, code).ConfigureAwait(false);
        if (!valid)
        {
            throw new InvalidOperationException("Invalid email verification code.");
        }

        if (!await HasClaimAsync(user).ConfigureAwait(false))
        {
            await userManager.AddClaimAsync(
                user, new Claim(TwoFactorClaims.EmailOtpEnabled, TwoFactorClaims.EnabledValue))
                .ConfigureAwait(false);
        }

        if (!await userManager.GetTwoFactorEnabledAsync(user).ConfigureAwait(false))
        {
            await userManager.SetTwoFactorEnabledAsync(user, true).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task DisableAsync(string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);

        if (await HasClaimAsync(user).ConfigureAwait(false))
        {
            await userManager.RemoveClaimAsync(
                user, new Claim(TwoFactorClaims.EmailOtpEnabled, TwoFactorClaims.EnabledValue))
                .ConfigureAwait(false);
        }

        // Clear the master switch and rotate the security stamp only when no other factor
        // remains. An authenticator key (key present) keeps 2FA active.
        string? key = await userManager.GetAuthenticatorKeyAsync(user).ConfigureAwait(false);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.SetTwoFactorEnabledAsync(user, false).ConfigureAwait(false);
            await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await FindUserAsync(userId).ConfigureAwait(false);
        return await HasClaimAsync(user).ConfigureAwait(false);
    }

    private async Task<bool> HasClaimAsync(LocalIdentity user) =>
        (await userManager.GetClaimsAsync(user).ConfigureAwait(false))
            .Any(c => c.Type == TwoFactorClaims.EmailOtpEnabled && c.Value == TwoFactorClaims.EnabledValue);

    private async Task<LocalIdentity> FindUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId).ConfigureAwait(false)
        ?? throw new InvalidOperationException($"User {userId} not found.");
}
#pragma warning restore GRSEC003
