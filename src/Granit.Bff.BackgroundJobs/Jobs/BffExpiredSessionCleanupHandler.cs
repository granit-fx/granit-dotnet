using Granit.Bff.BackgroundJobs.Internal;

namespace Granit.Bff.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="BffExpiredSessionCleanupJob"/>. Delegates to
/// <see cref="ExpiredSessionCleanupService"/> for session purge logic.
/// </summary>
internal static class BffExpiredSessionCleanupHandler
{
    public static Task HandleAsync(
        BffExpiredSessionCleanupJob _,
        ExpiredSessionCleanupService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
