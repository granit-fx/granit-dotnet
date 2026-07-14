using System.Diagnostics.CodeAnalysis;
using Granit.DataExchange.BackgroundJobs.Services;

namespace Granit.DataExchange.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="DataExchangeRetentionSweepJob"/> by delegating to
/// <see cref="RetentionSweepService"/> for the GDPR retention sweep.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class DataExchangeRetentionSweepHandler
{
    public static Task HandleAsync(
        DataExchangeRetentionSweepJob _,
        RetentionSweepService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
