namespace Granit.Core.Events;

/// <summary>
/// Integration event (ETO — Event Transfer Object) emitted automatically when an entity
/// implementing <c>IHasEntityEto&lt;TEto&gt;</c> is deleted (hard or soft) and the change is persisted.
/// </summary>
/// <typeparam name="TEto">The flat, serializable ETO type.</typeparam>
/// <param name="Eto">The serializable snapshot produced by <c>IHasEntityEto&lt;TEto&gt;.ToEto()</c>,
/// captured before <c>SaveChanges</c>.</param>
/// <remarks>
/// Dispatched by <c>EntityLifecycleEventInterceptor</c> in <c>SavingChanges</c> (before the
/// database transaction commits) so that Wolverine writes the outbox envelope atomically.
/// </remarks>
public sealed record EntityDeletedEto<TEto>(TEto Eto) : IIntegrationEvent
    where TEto : class;
