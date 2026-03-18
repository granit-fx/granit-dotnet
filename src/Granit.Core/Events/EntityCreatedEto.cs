namespace Granit.Core.Events;

/// <summary>
/// Integration event (ETO — Event Transfer Object) emitted automatically when an entity
/// implementing <c>IHasEntityEto&lt;TEto&gt;</c> is created and successfully persisted.
/// </summary>
/// <typeparam name="TEto">The flat, serializable ETO type.</typeparam>
/// <param name="Eto">The serializable snapshot produced by <c>IHasEntityEto&lt;TEto&gt;.ToEto()</c>.</param>
/// <remarks>
/// Dispatched by <c>EntityLifecycleEventInterceptor</c> in <c>SavingChanges</c> (before the
/// database transaction commits) so that Wolverine writes the outbox envelope atomically.
/// <para>
/// <strong>NEVER</strong> include EF Core entities or navigation properties in <typeparamref name="TEto"/>.
/// Use flat, JSON-serializable records only.
/// </para>
/// </remarks>
public sealed record EntityCreatedEto<TEto>(TEto Eto) : IIntegrationEvent
    where TEto : class;
