using OpenIddict.Abstractions;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictTokenCleanupJob"/>. Prunes expired tokens
/// and orphaned authorizations from the OpenIddict store.
/// </summary>
internal static partial class OpenIddictTokenCleanupHandler
{
    /// <summary>
    /// Prunes expired tokens and authorizations.
    /// </summary>
    public static async Task HandleAsync(
        OpenIddictTokenCleanupJob _,
        IOpenIddictTokenManager tokenManager,
        IOpenIddictAuthorizationManager authorizationManager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await tokenManager.PruneAsync(now, cancellationToken).ConfigureAwait(false);
        await authorizationManager.PruneAsync(now, cancellationToken).ConfigureAwait(false);
    }
}
