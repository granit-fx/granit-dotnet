using System.Security.Claims;
using Granit.Events;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using GranitExternalLoginInfo = Granit.Identity.Local.Services.ExternalLoginInfo;

namespace Granit.OpenIddict.Internal;

/// <summary>
/// <see cref="IExternalLoginService"/> implementation backed by ASP.NET Core Identity's
/// <see cref="UserManager{TUser}"/> external login support.
/// </summary>
internal sealed class AspNetExternalLoginService(
    UserManager<GranitUser> userManager,
    ExternalClaimsMapper claimsMapper,
    IDistributedEventBus eventBus,
    IOptions<GranitOpenIddictClientOptions> clientOptions) : IExternalLoginService
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<GranitExternalLoginInfo>> GetLoginsAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser? user = await userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return [];
        }

        IList<UserLoginInfo> logins = await userManager.GetLoginsAsync(user).ConfigureAwait(false);
        return logins
            .Select(l => new GranitExternalLoginInfo(l.LoginProvider, l.ProviderKey, l.ProviderDisplayName))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task AddLoginAsync(
        string userId, GranitExternalLoginInfo info, CancellationToken cancellationToken = default)
    {
        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        IdentityResult result = await userManager.AddLoginAsync(
            user,
            new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName))
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to add external login: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    /// <inheritdoc/>
    public async Task RemoveLoginAsync(
        string userId, string provider, string providerKey, CancellationToken cancellationToken = default)
    {
        GranitUser user = await userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        // Guard: cannot remove last login method if no password is set
        bool hasPassword = await userManager.HasPasswordAsync(user).ConfigureAwait(false);
        IList<UserLoginInfo> logins = await userManager.GetLoginsAsync(user).ConfigureAwait(false);

        if (!hasPassword && logins.Count <= 1)
        {
            throw new InvalidOperationException(
                "Cannot remove the last external login when no password is set. Set a password first.");
        }

        IdentityResult result = await userManager.RemoveLoginAsync(user, provider, providerKey)
            .ConfigureAwait(false);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to remove external login: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }

    /// <inheritdoc/>
    public async Task<ProcessCallbackResult> ProcessCallbackAsync(
        ClaimsPrincipal principal, string provider, CancellationToken cancellationToken = default)
    {
        // 1. Try to find an existing user by external login
        string? providerKey = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(providerKey))
        {
            throw new InvalidOperationException("External provider did not return a user identifier.");
        }

        GranitUser? existingUser = await userManager.FindByLoginAsync(provider, providerKey)
            .ConfigureAwait(false);

        if (existingUser is not null)
        {
            return new ProcessCallbackResult(existingUser.Id, false);
        }

        // 2. Try to find by email
        ExternalUserProperties props = claimsMapper.MapToUserProperties(principal, provider);

        if (!string.IsNullOrEmpty(props.Email))
        {
            existingUser = await userManager.FindByEmailAsync(props.Email).ConfigureAwait(false);

            if (existingUser is not null)
            {
                // Link the external provider to the existing account
                await userManager.AddLoginAsync(
                    existingUser,
                    new UserLoginInfo(provider, providerKey, provider))
                    .ConfigureAwait(false);

                return new ProcessCallbackResult(existingUser.Id, false);
            }
        }

        // 3. Auto-register if enabled
        if (!clientOptions.Value.AutoRegisterExternalUsers)
        {
            throw new InvalidOperationException(
                "Account not found. Contact your administrator.");
        }

        GranitUser newUser = new()
        {
            UserName = props.Email ?? props.UserName ?? providerKey,
            Email = props.Email,
            FirstName = props.FirstName,
            LastName = props.LastName,
            EmailConfirmed = true, // External provider already verified
        };

        IdentityResult createResult = await userManager.CreateAsync(newUser).ConfigureAwait(false);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"User creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        await userManager.AddLoginAsync(
            newUser,
            new UserLoginInfo(provider, providerKey, provider))
            .ConfigureAwait(false);

        await eventBus.PublishAsync(
            new UserRegisteredEto(newUser.Id, newUser.TenantId),
            cancellationToken).ConfigureAwait(false);

        return new ProcessCallbackResult(newUser.Id, true);
    }
}
