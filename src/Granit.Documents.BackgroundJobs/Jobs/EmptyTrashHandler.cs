namespace Granit.Documents.BackgroundJobs.Jobs;

/// <summary>Handler for <see cref="EmptyTrashJob"/> (F9.2).</summary>
public sealed class EmptyTrashHandler
{
    public static Task HandleAsync(
        EmptyTrashJob _,
        IDocumentMaintenanceService maintenance,
        CancellationToken cancellationToken) =>
        maintenance.EmptyTrashAsync(cancellationToken);
}
