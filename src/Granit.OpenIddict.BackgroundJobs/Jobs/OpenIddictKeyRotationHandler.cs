using Granit.OpenIddict.BackgroundJobs.Internal;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictKeyRotationJob"/>. Delegates to
/// <see cref="KeyRotationExecutionService"/> for signing key rotation logic.
/// </summary>
internal static class OpenIddictKeyRotationHandler
{
    public static Task HandleAsync(
        OpenIddictKeyRotationJob _,
        KeyRotationExecutionService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
