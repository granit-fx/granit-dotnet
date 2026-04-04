using Granit.OpenIddict.BackgroundJobs.Internal;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictIdleSessionEnforcementJob"/>. Delegates to
/// <see cref="IdleSessionEnforcementService"/> for idle session revocation logic.
/// </summary>
internal static class OpenIddictIdleSessionEnforcementHandler
{
    public static Task HandleAsync(
        OpenIddictIdleSessionEnforcementJob _,
        IdleSessionEnforcementService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
