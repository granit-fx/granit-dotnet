using Granit.Events;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IPasswordResetService"/> implementation backed by ASP.NET Core Identity.
/// Generates tokens via <see cref="UserManager{TUser}"/> and publishes events
/// via <see cref="IDistributedEventBus"/>.
/// </summary>
#pragma warning disable GRSEC003 // Password reset token handling, not stored secrets
internal sealed class AspNetPasswordResetService(
    UserManager<GranitUser> userManager,
    IDistributedEventBus eventBus) : IPasswordResetService
{
    /// <inheritdoc/>
    public async Task<bool> RequestResetAsync(string email, CancellationToken cancellationToken = default)
    {
        GranitUser? user = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        string token = await userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new PasswordResetRequestedEto(user.Id, email, token, user.TenantId),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async Task ResetPasswordAsync(
        string userId, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        IdentityResult result = await userManager.ResetPasswordAsync(user, token, newPassword)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Password reset failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // ResetPasswordAsync resets AccessFailedCount but does NOT clear LockoutEnd.
        // Explicitly unlock so a locked-out user regains access after resetting their password.
        // Also reset the exponential backoff counter to prevent the next typo from
        // triggering a long lockout duration.
        if (user.LockoutEnd is not null)
        {
            user.ConsecutiveLockouts = 0;
            await userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
            await userManager.UpdateAsync(user).ConfigureAwait(false);
        }
    }
}
#pragma warning restore GRSEC003
