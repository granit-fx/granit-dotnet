namespace Granit.Auditing.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="AuditRetentionCleanupJob"/>. Delegates to
/// <see cref="IAuditRetentionCleanupService"/> for the per-category purge logic.
/// </summary>
public class AuditRetentionCleanupHandler
{
    public static Task HandleAsync(
        AuditRetentionCleanupJob _,
        IAuditRetentionCleanupService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
