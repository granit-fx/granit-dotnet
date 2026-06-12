using System.Diagnostics.CodeAnalysis;
using Granit.Indexing.BackgroundJobs.Services;

namespace Granit.Indexing.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="RebuildIndexJob{TKey}"/>. Delegates to
/// <see cref="RebuildIndexService{TKey}"/>.
/// </summary>
/// <remarks>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c> — the class is
/// therefore <c>public</c> with a public constructor, the method is
/// <c>public static</c>. See CLAUDE.md §Wolverine handlers.
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class RebuildIndexHandler
{
    public static Task HandleAsync<TKey>(
        RebuildIndexJob<TKey> job,
        RebuildIndexService<TKey> service,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(service);
        return service.ExecuteAsync(job.TenantId, cancellationToken);
    }
}
