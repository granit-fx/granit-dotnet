using Granit.Parties.Deduplication.BackgroundJobs.Services;

namespace Granit.Parties.Deduplication.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="PartyDuplicateScanJob"/>. Delegates to
/// <see cref="PartyDuplicateScanService"/>; lives as a thin shim so the job record can
/// stay a value type free of orchestration.
/// </summary>
public class PartyDuplicateScanHandler
{
    public static Task HandleAsync(
        PartyDuplicateScanJob _,
        IPartyDuplicateScanService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
