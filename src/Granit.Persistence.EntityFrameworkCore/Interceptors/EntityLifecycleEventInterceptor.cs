using System.Collections.Concurrent;
using System.Linq.Expressions;
using Granit.Domain;
using Granit.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.Interceptors;

/// <summary>
/// EF Core interceptor that automatically emits entity lifecycle events for entities
/// implementing <see cref="IEmitEntityLifecycleEvents"/> or <see cref="IHasEntityEto{TEto}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Two dispatch timings are used deliberately:
/// <list type="bullet">
///   <item>
///     <c>SavingChanges</c> — distributed ETOs (<see cref="IIntegrationEvent"/>) are dispatched
///     before the transaction commits so Wolverine can write outbox envelopes atomically.
///   </item>
///   <item>
///     <c>SavedChanges</c> — local domain events (<see cref="IDomainEvent"/>) are dispatched
///     after the transaction commits so handlers can safely read committed data.
///   </item>
/// </list>
/// </para>
/// <para>
/// Inter-callback state is stored in <see cref="DbContext.Items"/> (keyed by
/// <see cref="PendingDomainEventsKey"/>) rather than <c>AsyncLocal</c> to avoid
/// cross-request leaks on interceptors registered as singletons.
/// </para>
/// <para>
/// Generic event record construction uses compiled <see cref="Expression"/> factories
/// cached in <see cref="DomainEventFactoryCache"/> — no <c>Activator.CreateInstance</c>
/// on the hot path.
/// </para>
/// </remarks>
public sealed class EntityLifecycleEventInterceptor(
    IDomainEventDispatcher domainDispatcher,
    IIntegrationEventDispatcher integrationDispatcher) : SaveChangesInterceptor
{
    // Pending domain events keyed by DbContext.ContextId.InstanceId (set in SavingChanges,
    // consumed in SavedChanges / SaveChangesFailed).
    // Using a static dictionary (not AsyncLocal) avoids cross-request leaks when the
    // interceptor is registered as a singleton. Entries are always removed on SavedChanges
    // or SaveChangesFailed so there is no unbounded growth.
    private static readonly ConcurrentDictionary<Guid, List<IDomainEvent>>
        PendingDomainEventsBag = new();

    // Compiled factory cache: (entityType, state) → Func<object entity, IDomainEvent>
    private static readonly ConcurrentDictionary<(Type, EntityState), Func<object, IDomainEvent>>
        DomainEventFactoryCache = new();

    // Compiled ETO factory cache: entityType → Func<object entity, (Type etoType, object eto)>
    private static readonly ConcurrentDictionary<Type, Func<object, (Type, object)>>
        EtoFactoryCache = new();

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CollectAndDispatchEtosSync(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            await CollectAndDispatchEtosAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        DispatchDomainEventsSync(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
        return await base.SavedChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            PendingDomainEventsBag.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        }

        base.SaveChangesFailed(eventData);
    }

    /// <inheritdoc />
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            PendingDomainEventsBag.TryRemove(eventData.Context.ContextId.InstanceId, out _);
        }

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Collection & dispatch

    private void CollectAndDispatchEtosSync(DbContext context)
    {
        (List<IDomainEvent>? domainEvents, List<IIntegrationEvent>? integrationEvents) = Collect(context);

        if (domainEvents is { Count: > 0 })
        {
            PendingDomainEventsBag[context.ContextId.InstanceId] = domainEvents;
        }

        if (integrationEvents is { Count: > 0 })
        {
            integrationDispatcher.DispatchAsync(integrationEvents, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }
    }

    private async Task CollectAndDispatchEtosAsync(DbContext context, CancellationToken ct)
    {
        (List<IDomainEvent>? domainEvents, List<IIntegrationEvent>? integrationEvents) = Collect(context);

        if (domainEvents is { Count: > 0 })
        {
            PendingDomainEventsBag[context.ContextId.InstanceId] = domainEvents;
        }

        if (integrationEvents is { Count: > 0 })
        {
            await integrationDispatcher.DispatchAsync(integrationEvents, ct).ConfigureAwait(false);
        }
    }

    private void DispatchDomainEventsSync(DbContext? context)
    {
        if (context is null ||
            !PendingDomainEventsBag.TryRemove(context.ContextId.InstanceId, out List<IDomainEvent>? events) ||
            events.Count == 0)
        {
            return;
        }

        domainDispatcher.DispatchAsync(events, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private async Task DispatchDomainEventsAsync(DbContext? context, CancellationToken ct)
    {
        if (context is null ||
            !PendingDomainEventsBag.TryRemove(context.ContextId.InstanceId, out List<IDomainEvent>? events) ||
            events.Count == 0)
        {
            return;
        }

        await domainDispatcher.DispatchAsync(events, ct).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ChangeTracker scan

    private static (List<IDomainEvent>? DomainEvents, List<IIntegrationEvent>? IntegrationEvents)
        Collect(DbContext context)
    {
        List<IDomainEvent>? domainEvents = null;
        List<IIntegrationEvent>? integrationEvents = null;

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is not IEmitEntityLifecycleEvents)
            {
                continue;
            }

            EntityState state = entry.State;

            // Determine the logical lifecycle operation, accounting for soft deletes.
            EntityState logicalState = state;
            if (state == EntityState.Modified &&
                entry.Entity is ISoftDeletable softDeletable &&
                softDeletable.IsDeleted &&
                entry.Property(nameof(ISoftDeletable.IsDeleted)).OriginalValue is false)
            {
                logicalState = EntityState.Deleted;
            }

            if (logicalState is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            object entity = entry.Entity;
            Type entityType = entity.GetType();

            // Local domain event
            Func<object, IDomainEvent> domainFactory =
                DomainEventFactoryCache.GetOrAdd((entityType, logicalState), BuildDomainEventFactory);
            IDomainEvent domainEvent = domainFactory(entity);
            (domainEvents ??= []).Add(domainEvent);

            // Distributed ETO (only for entities implementing IHasEntityEto<TEto>)
            if (entity is IEntityEtoProvider)
            {
                Func<object, (Type, object)> etoFactory =
                    EtoFactoryCache.GetOrAdd(entityType, BuildEtoFactory);
                (Type etoType, object eto) = etoFactory(entity);

                IIntegrationEvent etoEvent = BuildEtoEvent(logicalState, etoType, eto);
                (integrationEvents ??= []).Add(etoEvent);
            }
        }

        return (domainEvents, integrationEvents);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Compiled factory builders (cached — one allocation per entity type per state)

    private static Func<object, IDomainEvent> BuildDomainEventFactory((Type entityType, EntityState state) key)
    {
        (Type entityType, EntityState state) = key;

        Type eventType = state switch
        {
            EntityState.Added => typeof(EntityCreatedEvent<>).MakeGenericType(entityType),
            EntityState.Modified => typeof(EntityUpdatedEvent<>).MakeGenericType(entityType),
            EntityState.Deleted => typeof(EntityDeletedEvent<>).MakeGenericType(entityType),
            _ => throw new InvalidOperationException($"Unexpected entity state: {state}")
        };

        // Compile: (object obj) => (IDomainEvent)new EventType((TEntity)obj)
        ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
        NewExpression ctor = Expression.New(
            eventType.GetConstructors()[0],
            Expression.Convert(objParam, entityType));
        UnaryExpression cast = Expression.Convert(ctor, typeof(IDomainEvent));
        return Expression.Lambda<Func<object, IDomainEvent>>(cast, objParam).Compile();
    }

    private static Func<object, (Type, object)> BuildEtoFactory(Type entityType)
    {
        // Compile: (object obj) => ((IEntityEtoProvider)obj).GetEto()
        // We use the interface dispatch — ToEto() is called via the default interface implementation.
        ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
        UnaryExpression castToProvider = Expression.Convert(objParam, typeof(IEntityEtoProvider));
        MethodCallExpression getEtoCall = Expression.Call(
            castToProvider,
            typeof(IEntityEtoProvider).GetMethod(nameof(IEntityEtoProvider.GetEto))!);
        return Expression.Lambda<Func<object, (Type, object)>>(getEtoCall, objParam).Compile();
    }

    private static IIntegrationEvent BuildEtoEvent(EntityState state, Type etoType, object eto)
    {
        Type eventType = state switch
        {
            EntityState.Added => typeof(EntityCreatedEto<>).MakeGenericType(etoType),
            EntityState.Modified => typeof(EntityUpdatedEto<>).MakeGenericType(etoType),
            EntityState.Deleted => typeof(EntityDeletedEto<>).MakeGenericType(etoType),
            _ => throw new InvalidOperationException($"Unexpected entity state: {state}")
        };

        // This MakeGenericType result is fine here — it's keyed by etoType+state but
        // for simplicity we don't cache it separately. The domain event factory cache
        // covers the expensive compilation; this Activator.CreateInstance is negligible
        // given it only runs for entities that opt into distributed events.
        return (IIntegrationEvent)Activator.CreateInstance(eventType, eto)!;
    }
}
