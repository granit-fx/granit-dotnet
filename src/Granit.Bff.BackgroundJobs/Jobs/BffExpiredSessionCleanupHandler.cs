namespace Granit.Bff.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="BffExpiredSessionCleanupJob"/>. Delegates to
/// <see cref="IExpiredSessionCleanupService"/> for session purge logic.
/// </summary>
public class BffExpiredSessionCleanupHandler
{
    public static Task HandleAsync(
        BffExpiredSessionCleanupJob _,
        IExpiredSessionCleanupService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
