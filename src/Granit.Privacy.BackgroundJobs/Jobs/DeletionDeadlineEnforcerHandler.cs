using System.Diagnostics.CodeAnalysis;
using Granit.Privacy.BackgroundJobs.Services;

namespace Granit.Privacy.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="DeletionDeadlineEnforcerJob"/> by delegating to
/// <see cref="DeletionDeadlineEnforcementService"/> for expired deferred deletion processing.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class DeletionDeadlineEnforcerHandler
{
    public static Task HandleAsync(
        DeletionDeadlineEnforcerJob _,
        DeletionDeadlineEnforcementService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
