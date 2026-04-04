using Granit.OpenIddict.BackgroundJobs.Internal;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictTokenCleanupJob"/>. Delegates to
/// <see cref="TokenCleanupService"/> for token and authorization pruning.
/// </summary>
internal static class OpenIddictTokenCleanupHandler
{
    public static Task HandleAsync(
        OpenIddictTokenCleanupJob _,
        TokenCleanupService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
