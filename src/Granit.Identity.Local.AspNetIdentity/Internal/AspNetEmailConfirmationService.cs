using Granit.Events;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IEmailConfirmationService"/> implementation backed by ASP.NET Core Identity.
/// Generates tokens via <see cref="UserManager{TUser}"/> and publishes
/// <see cref="EmailConfirmationRequestedEto"/> via <see cref="IDistributedEventBus"/>.
/// </summary>
internal sealed class AspNetEmailConfirmationService(
    UserManager<LocalIdentity> userManager,
    IDistributedEventBus eventBus) : IEmailConfirmationService
{
    /// <inheritdoc/>
    public async Task SendConfirmationEmailAsync(
        string userId, string email, CancellationToken cancellationToken = default)
    {
        LocalIdentity? user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        string token = await userManager.GenerateEmailConfirmationTokenAsync(user)
            .ConfigureAwait(false);

        await eventBus.PublishAsync(
            new EmailConfirmationRequestedEto(user.Id, token, user.TenantId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmAsync(
        string userId, string token, CancellationToken cancellationToken = default)
    {
        LocalIdentity? user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        IdentityResult result = await userManager.ConfirmEmailAsync(user, token)
            .ConfigureAwait(false);

        return result.Succeeded;
    }
}
