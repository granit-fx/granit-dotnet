using System.Diagnostics.CodeAnalysis;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion.Events;

namespace Granit.Indexing.Privacy;

/// <summary>
/// Wolverine handler that fans <see cref="PersonalDataDeletionRequestedEto"/> into every
/// registered <see cref="IIndexedDataEraser"/>. Backend-agnostic — works with the EF
/// tsvector store, Elasticsearch, or any future vector backend that ships its own
/// eraser.
/// </summary>
/// <remarks>
/// <para>
/// <b>Provider role.</b> One of the providers fanning out from <c>Granit.Privacy</c>'s
/// deletion request flow. Each registered eraser is awaited sequentially; failures are
/// surfaced (do not swallow) so Wolverine's retry policy can re-run the handler. Partial
/// completion is acceptable because every eraser is idempotent: a re-run after a transient
/// failure erases only what remains.
/// </para>
/// <para>
/// <b>Visibility.</b> Wolverine discovers handlers via <c>Assembly.ExportedTypes</c>; the
/// class is therefore <c>public</c> with a public constructor, and the handle method is
/// <c>public static</c>.
/// </para>
/// </remarks>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class PersonalDataDeletionHandler
{
    public static async Task Handle(
        PersonalDataDeletionRequestedEto @event,
        IEnumerable<IIndexedDataEraser> erasers,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(erasers);
        ArgumentNullException.ThrowIfNull(currentTenant);

        Guid? tenantId = @event.TenantId ?? currentTenant.Id;

        foreach (IIndexedDataEraser eraser in erasers)
        {
            await eraser.EraseAsync(tenantId, @event.UserId, cancellationToken).ConfigureAwait(false);
        }
    }
}
