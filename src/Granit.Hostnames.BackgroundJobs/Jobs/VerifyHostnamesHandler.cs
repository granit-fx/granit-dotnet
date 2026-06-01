using Granit.Hostnames.BackgroundJobs.Services;

namespace Granit.Hostnames.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="VerifyHostnamesJob"/> by delegating to
/// <see cref="HostnameVerificationBatchService"/>.
/// </summary>
public sealed class VerifyHostnamesHandler
{
    public static Task HandleAsync(
        VerifyHostnamesJob _,
        HostnameVerificationBatchService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
