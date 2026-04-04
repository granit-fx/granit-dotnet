using Granit.OpenIddict.BackgroundJobs.Services;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="OpenIddictIdleSessionEnforcementJob"/>. Delegates to
/// <see cref="IdleSessionEnforcementService"/> for idle session revocation logic.
/// </summary>
public class OpenIddictIdleSessionEnforcementHandler
{
    public static Task HandleAsync(
        OpenIddictIdleSessionEnforcementJob _,
        IdleSessionEnforcementService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
