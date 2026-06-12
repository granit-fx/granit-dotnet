using System.Diagnostics.CodeAnalysis;
using Granit.BackgroundJobs.Abstractions;
using Granit.Privacy.DataExport.Events;

namespace Granit.Privacy.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler that bridges the saga's terminal
/// <see cref="ExportCompletedEto"/> to the background-job assembly path.
/// Publishes a <see cref="PrivacyExportAssemblyJob"/> carrying the event verbatim;
/// the actual sharded ZIP assembly runs out of band of the saga's message handler.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a bridge.</b> Keeping the handler one-line lets the saga publish a single
/// terminal event consumers can subscribe to (notifications, audit, …) while the
/// long-running assembly work moves to a durable background job — the saga itself
/// stays small and predictable.
/// </para>
/// <para>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the class is
/// <c>public</c> with a public constructor, the method is <c>public static</c>.
/// See CLAUDE.md §Wolverine handlers.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public sealed class PrivacyExportCompletedHandler
{
    public static Task HandleAsync(
        ExportCompletedEto completion,
        IBackgroundJobDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(completion);
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.PublishAsync(new PrivacyExportAssemblyJob(completion), cancellationToken: cancellationToken);
    }
}
