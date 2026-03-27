using Granit.Events;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="IAccountDeletionService"/> implementation backed by ASP.NET Core Identity.
/// Soft-deletes the user, revokes all active tokens, and publishes <see cref="AccountDeletedEto"/>
/// via <see cref="IDistributedEventBus"/>.
/// </summary>
internal sealed class AspNetAccountDeletionService(
    UserManager<GranitUser> userManager,
    IOpenIddictTokenManager tokenManager,
    IDistributedEventBus eventBus,
    IClock clock) : IAccountDeletionService
{
    /// <inheritdoc/>
    public async Task InitiateAsync(string userId, CancellationToken cancellationToken = default)
    {
        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        // Soft-delete
        user.IsDeleted = true;
        user.DeletedAt = clock.Now;
        user.DeletedBy = userId;

        IdentityResult result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Account deletion failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        // Lock the account to prevent login
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue).ConfigureAwait(false);

        // Invalidate the security stamp so existing cookie/JWT claims validation fails
        await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);

        // Revoke all active OpenIddict tokens for this user (GDPR Art. 17 — immediate access termination)
        await foreach (object token in tokenManager.FindBySubjectAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            await tokenManager.TryRevokeAsync(token, cancellationToken).ConfigureAwait(false);
        }

        // Publish integration event for downstream cleanup
        await eventBus.PublishAsync(
            new AccountDeletedEto(user.Id, user.TenantId),
            cancellationToken).ConfigureAwait(false);
    }
}
