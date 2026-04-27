using Granit.Authentication.ApiKeys.BackgroundJobs.Services;

namespace Granit.Authentication.ApiKeys.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler that delegates <see cref="ExpiringApiKeyScannerJob"/> to
/// <see cref="ExpiringApiKeyScannerService"/>.
/// </summary>
public class ExpiringApiKeyScannerHandler
{
    public static Task HandleAsync(
        ExpiringApiKeyScannerJob _,
        ExpiringApiKeyScannerService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
