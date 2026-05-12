namespace Granit.Documents.BackgroundJobs.Jobs;

/// <summary>Handler for <see cref="OrphanDocumentCleanupJob"/> (F9.1).</summary>
public sealed class OrphanDocumentCleanupHandler
{
    public static Task HandleAsync(
        OrphanDocumentCleanupJob _,
        IDocumentMaintenanceService maintenance,
        CancellationToken cancellationToken) =>
        maintenance.CleanupOrphanBlobsAsync(cancellationToken);
}
