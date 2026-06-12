using System.Diagnostics.CodeAnalysis;

namespace Granit.Auditing.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="AuditRetentionCleanupJob"/>. Delegates to
/// <see cref="IAuditRetentionCleanupService"/> for the per-category purge logic.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class AuditRetentionCleanupHandler
{
    public static Task HandleAsync(
        AuditRetentionCleanupJob _,
        IAuditRetentionCleanupService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
