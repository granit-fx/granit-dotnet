using Granit.Webhooks.BackgroundJobs.Services;

namespace Granit.Webhooks.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="SigningKeyRotationScanJob"/> by delegating to
/// <see cref="SigningKeyRotationScanService"/>.
/// </summary>
public class SigningKeyRotationScanHandler
{
    public static Task HandleAsync(
        SigningKeyRotationScanJob _,
        SigningKeyRotationScanService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
