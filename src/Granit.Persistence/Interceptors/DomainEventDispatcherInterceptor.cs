using System.Collections.Concurrent;
using Granit.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that collects and dispatches events from tracked aggregate roots.
/// </summary>
/// <remarks>
/// <para>
/// Two dispatch timings are used deliberately:
/// <list type="bullet">
///   <item>
///     <c>SavingChanges</c> — integration events (<see cref="IIntegrationEvent"/>) added via
///     <c>AggregateRoot.AddDistributedEvent()</c> are dispatched before the transaction commits
///     so Wolverine can write outbox envelopes atomically.
///   </item>
///   <item>
///     <c>SavedChanges</c> — domain events (<see cref="IDomainEvent"/>) added via
///     <c>AggregateRoot.AddDomainEvent()</c> are dispatched after the transaction commits
///     so handlers can safely read committed data.
///   </item>
/// </list>
/// </para>
/// <para>
/// Inter-callback state is stored in a static <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by <c>DbContext.ContextId.InstanceId</c> rather than <c>AsyncLocal</c> to avoid
/// cross-request leaks on interceptors registered as singletons or used across
/// concurrent <c>SaveChanges</c> calls on the same async flow.
/// </para>
/// </remarks>
public sealed class DomainEventDispatcherInterceptor(
    IDomainEventDispatcher domainDispatcher,
    IIntegrationEventDispatcher integrationDispatcher) : SaveChangesInterceptor
{
    // Pending domain events keyed by DbContext.ContextId.InstanceId (set in SavingChanges,
    // consumed in SavedChanges / SaveChangesFailed).
    private static readonly ConcurrentDictionary<Guid, List<IDomainEvent>>
        PendingDomainEventsBag = new();

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            CollectAndDispatchIntegrationEventsSync(eventData.Context);
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
            await CollectAndDispatchIntegrationEventsAsync(eventData.Context, cancellationToken).ConfigureAwait(false);
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

    private void CollectAndDispatchIntegrationEventsSync(DbContext context)
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

    private async Task CollectAndDispatchIntegrationEventsAsync(DbContext context, CancellationToken ct)
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

        foreach (object entity in context.ChangeTracker.Entries().Select(entry => entry.Entity))
        {
            if (entity is IDomainEventSource domainSource && domainSource.DomainEvents.Count > 0)
            {
                (domainEvents ??= []).AddRange(domainSource.DomainEvents);
                domainSource.ClearDomainEvents();
            }

            if (entity is IIntegrationEventSource integrationSource && integrationSource.IntegrationEvents.Count > 0)
            {
                (integrationEvents ??= []).AddRange(integrationSource.IntegrationEvents);
                integrationSource.ClearIntegrationEvents();
            }
        }

        return (domainEvents, integrationEvents);
    }
}
