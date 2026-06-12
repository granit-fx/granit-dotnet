using System.Diagnostics.CodeAnalysis;
using Granit.Hostnames.BackgroundJobs.Services;

namespace Granit.Hostnames.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="VerifyHostnamesJob"/> by delegating to
/// <see cref="HostnameVerificationBatchService"/>.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class VerifyHostnamesHandler
{
    public static Task HandleAsync(
        VerifyHostnamesJob _,
        HostnameVerificationBatchService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
