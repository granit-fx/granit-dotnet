namespace Granit.Documents.BackgroundJobs.Jobs;

/// <summary>Handler for <see cref="QuotaRecomputeJob"/> (F9.3).</summary>
public sealed class QuotaRecomputeHandler
{
    public static Task HandleAsync(
        QuotaRecomputeJob _,
        IDocumentMaintenanceService maintenance,
        CancellationToken cancellationToken) =>
        maintenance.RecomputeQuotasAsync(cancellationToken);
}
