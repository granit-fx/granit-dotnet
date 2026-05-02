using Granit.Events;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IEmailChangeService"/> implementation backed by ASP.NET Core Identity.
/// Generates tokens via <see cref="UserManager{TUser}.GenerateChangeEmailTokenAsync"/>
/// and publishes <see cref="EmailChangeRequestedEto"/> for downstream notification handlers.
/// </summary>
internal sealed class AspNetEmailChangeService(
    UserManager<LocalIdentity> userManager,
    IDistributedEventBus eventBus) : IEmailChangeService
{
    /// <inheritdoc/>
    public async Task<bool> RequestChangeAsync(
        string userId, string newEmail, CancellationToken cancellationToken = default)
    {
        LocalIdentity? user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        string token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail)
            .ConfigureAwait(false);

        await eventBus.PublishAsync(
            new EmailChangeRequestedEto(
                user.Id,
                user.Email ?? string.Empty,
                newEmail,
                token,
                user.TenantId),
            cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmChangeAsync(
        string userId, string newEmail, string token, CancellationToken cancellationToken = default)
    {
        LocalIdentity? user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        IdentityResult result = await userManager.ChangeEmailAsync(user, newEmail, token)
            .ConfigureAwait(false);

        if (result.Succeeded)
        {
            // Keep username in sync with email (standard convention)
            await userManager.SetUserNameAsync(user, newEmail).ConfigureAwait(false);
        }

        return result.Succeeded;
    }
}
