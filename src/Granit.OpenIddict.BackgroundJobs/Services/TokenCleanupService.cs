using OpenIddict.Abstractions;

namespace Granit.OpenIddict.BackgroundJobs.Services;

/// <summary>
/// Prunes expired tokens and orphaned authorizations from the OpenIddict store.
/// </summary>
public sealed class TokenCleanupService(
    IOpenIddictTokenManager tokenManager,
    IOpenIddictAuthorizationManager authorizationManager,
    TimeProvider timeProvider)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await tokenManager.PruneAsync(now, cancellationToken).ConfigureAwait(false);
        await authorizationManager.PruneAsync(now, cancellationToken).ConfigureAwait(false);
    }
}
