using Granit.Core.Events;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.Events;
using Granit.OpenIddict.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="IAccountDeletionService"/> implementation backed by ASP.NET Core Identity.
/// Soft-deletes the user and publishes <see cref="AccountDeletedEto"/>
/// via <see cref="IDistributedEventBus"/>.
/// </summary>
internal sealed class AspNetAccountDeletionService(
    UserManager<GranitUser> userManager,
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

        // Publish integration event for downstream cleanup
        await eventBus.PublishAsync(
            new AccountDeletedEto(user.Id, user.TenantId),
            cancellationToken).ConfigureAwait(false);
    }
}
