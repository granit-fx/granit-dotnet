using Granit.Events;

namespace Granit.Indexing.BackgroundJobs.Events;

/// <summary>
/// Raised at the very start of a rebuild run, before the first key is read. Local
/// (<see cref="IDomainEvent"/>) — handler chain runs in-process so audit subscribers
/// can persist the run intent atomically with checkpoint setup.
/// </summary>
/// <param name="TenantId">Target tenant. <c>null</c> = cross-tenant rebuild.</param>
/// <param name="SourceName"><see cref="IIndexedEntrySource{TKey}.Name"/> being rebuilt.</param>
/// <param name="KeyTypeName">Closed generic <c>TKey</c> discriminator (e.g. <c>"System.Guid"</c>).</param>
/// <param name="ResumedFromCheckpoint">
/// <c>true</c> when a prior checkpoint was present and the run resumes past it;
/// <c>false</c> when starting from the beginning.
/// </param>
/// <param name="DispatchedByUserId">
/// Dispatching principal's user id, when one is associated with the current scope.
/// <c>null</c> for jobs dispatched outside a user context (host start-up, scheduled
/// triggers without a principal).
/// </param>
public sealed record IndexRebuildStartedEvent(
    Guid? TenantId,
    string SourceName,
    string KeyTypeName,
    bool ResumedFromCheckpoint,
    string? DispatchedByUserId) : IDomainEvent;
