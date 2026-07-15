using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// <see cref="IAccountDeletionService"/> implementation backed by ASP.NET Core Identity.
/// Soft-deletes the user and revokes all active tokens.
/// </summary>
/// <remarks>
/// The GDPR Art. 17 erasure event (<c>AccountDeletedEto</c>) is <em>not</em> published inline: the
/// OpenIddict DbContext is not enrolled in the Wolverine outbox, so a post-commit publish could be
/// lost to a crash. The soft-delete row (<see cref="LocalIdentity.IsDeleted"/> with a null
/// <see cref="LocalIdentity.DeletionEventDispatchedAt"/>) is the durable intent; the recurring
/// reconciler (<c>AccountDeletionEtoReconciler</c>) publishes the event at-least-once from it.
/// </remarks>
internal sealed class AspNetAccountDeletionService(
    UserManager<LocalIdentity> userManager,
    IOpenIddictTokenManager tokenManager,
    IClock clock) : IAccountDeletionService
{
    /// <inheritdoc/>
    public async Task InitiateAsync(string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
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

        // The AccountDeletedEto is published by AccountDeletionEtoReconciler from the durable
        // soft-delete row — see the class remarks — so a crash here cannot drop the erasure event.
    }
}
