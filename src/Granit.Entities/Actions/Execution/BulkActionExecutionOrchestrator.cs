using System.Reflection;
using System.Text.Json;
using Granit.Domain;
using Granit.Entities.Actions.Execution;
using Granit.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Entities.Actions.Execution;

/// <summary>
/// Type-erased dispatcher between the bulk endpoint (non-generic, knows the
/// entity by name → CLR type) and the generic
/// <see cref="IEntityActionExecutor{TEntity}"/> / <see cref="IBulkActionExecutor{TEntity}"/>
/// surface (ADR-056). Resolves the executor through DI, dispatches the run,
/// then emits exactly one <see cref="EntityBulkUpdatedEvent{TEntity}"/> via
/// <see cref="ILocalEventBus"/> when the entity implements
/// <see cref="IEmitEntityLifecycleEvents"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton — owns no per-request state. All scoped
/// resolution goes through the <see cref="IServiceProvider"/> handed in
/// per call (the endpoint forwards <c>HttpContext.RequestServices</c>).
/// </remarks>
public sealed partial class BulkActionExecutionOrchestrator(ILogger<BulkActionExecutionOrchestrator> logger)
{
    private static readonly MethodInfo DispatchGenericMethod = typeof(BulkActionExecutionOrchestrator)
        .GetMethod(nameof(DispatchGenericAsync), BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException(
            "BulkActionExecutionOrchestrator.DispatchGenericAsync method not found via reflection.");

    /// <summary>
    /// Resolves the executor for <paramref name="entityType"/> and dispatches the
    /// run. The caller (the bulk endpoint) is responsible for permission gating
    /// and request-shape validation — this method is purely the dispatch seam.
    /// </summary>
    /// <param name="entityType">CLR type of the targeted entity (from the registry).</param>
    /// <param name="serverExecutorType">CLR type of the registered per-row executor (from the descriptor).</param>
    /// <param name="ids">Primary keys to act upon. Guaranteed non-empty by the endpoint.</param>
    /// <param name="payload">Caller-supplied JSON payload, forwarded verbatim.</param>
    /// <param name="scopedServices">Per-request <see cref="IServiceProvider"/>.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>
    /// The framework-level outcome — <see cref="BulkActionDispatchResult.Affected"/>
    /// counts the rows the executor accepted, <see cref="BulkActionDispatchResult.Failures"/>
    /// carries the per-row reasons.
    /// </returns>
    public Task<BulkActionDispatchResult> DispatchAsync(
        Type entityType,
        Type serverExecutorType,
        IReadOnlyList<Guid> ids,
        JsonElement payload,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(serverExecutorType);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(scopedServices);

        MethodInfo closed = DispatchGenericMethod.MakeGenericMethod(entityType);
        return (Task<BulkActionDispatchResult>)closed.Invoke(
            this,
            [serverExecutorType, ids, payload, scopedServices, cancellationToken])!;
    }

    private async Task<BulkActionDispatchResult> DispatchGenericAsync<TEntity>(
        Type serverExecutorType,
        IReadOnlyList<Guid> ids,
        JsonElement payload,
        IServiceProvider scopedServices,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        BulkActionExecutionResult<TEntity> result;

        // Prefer the bulk-friendly executor when registered — one query, one save.
        IBulkActionExecutor<TEntity>? bulk = scopedServices.GetService<IBulkActionExecutor<TEntity>>();
        if (bulk is not null)
        {
            result = await bulk.ExecuteBulkAsync(ids, payload, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Fallback: loop the per-row executor. Same observable contract;
            // N queries instead of one.
            object executorInstance = scopedServices.GetRequiredService(serverExecutorType);
            if (executorInstance is not IEntityActionExecutor<TEntity> executor)
            {
                throw new InvalidOperationException(
                    $"Server executor '{serverExecutorType.FullName}' does not implement "
                    + $"IEntityActionExecutor<{typeof(TEntity).Name}>. Check the .ServerExecutor<>() "
                    + "declaration on the action.");
            }

            List<TEntity> affected = new(ids.Count);
            List<BulkActionFailure> failures = [];
            foreach (Guid id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EntityActionExecutionResult<TEntity> row = await executor
                    .ExecuteAsync(id, payload, cancellationToken)
                    .ConfigureAwait(false);

                if (row.AffectedEntity is { } entity)
                {
                    affected.Add(entity);
                }
                else
                {
                    failures.Add(new BulkActionFailure(id, row.FailureReason ?? "Unspecified"));
                }
            }

            result = new BulkActionExecutionResult<TEntity>(affected, failures);
        }

        // Emit the batched lifecycle event when the entity opted in. Entities
        // outside the IEmitEntityLifecycleEvents constraint silently skip — the
        // ADR documents this as a non-fatal manifest / persistence mismatch.
        if (result.AffectedEntities.Count > 0 && typeof(IEmitEntityLifecycleEvents).IsAssignableFrom(typeof(TEntity)))
        {
            ILocalEventBus? eventBus = scopedServices.GetService<ILocalEventBus>();
            if (eventBus is null)
            {
                LogNoEventBus(logger, typeof(TEntity).FullName ?? typeof(TEntity).Name);
            }
            else
            {
                // Activate the open-generic EntityBulkUpdatedEvent<TLifecycle>
                // through reflection: TEntity satisfies the lifecycle marker but
                // the generic constraint sits on a different type parameter.
                Type lifecycleEventType = typeof(EntityBulkUpdatedEvent<>).MakeGenericType(typeof(TEntity));
                object evt = Activator.CreateInstance(lifecycleEventType, result.AffectedEntities)!;
                await PublishViaReflectionAsync(eventBus, evt, lifecycleEventType, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return new BulkActionDispatchResult(
            Affected: result.AffectedEntities.Count,
            Failures: result.Failures);
    }

    private static Task PublishViaReflectionAsync(
        ILocalEventBus bus, object evt, Type eventType, CancellationToken cancellationToken)
    {
        MethodInfo open = typeof(ILocalEventBus).GetMethod(nameof(ILocalEventBus.PublishAsync))!;
        MethodInfo closed = open.MakeGenericMethod(eventType);
        return (Task)closed.Invoke(bus, [evt, cancellationToken])!;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Bulk action affected rows for entity '{Entity}' but no ILocalEventBus is registered; skipping EntityBulkUpdatedEvent emission.")]
    private static partial void LogNoEventBus(ILogger logger, string entity);
}

/// <summary>
/// Framework-level outcome of <see cref="BulkActionExecutionOrchestrator.DispatchAsync"/>.
/// Mirrors the public <c>BulkActionResponse</c> shape exposed by the endpoint
/// but stays in the base module — the endpoint layer maps it to its DTO.
/// </summary>
/// <param name="Affected">Number of rows the executor accepted.</param>
/// <param name="Failures">Per-row failures reported by the executor.</param>
public sealed record BulkActionDispatchResult(
    int Affected,
    IReadOnlyList<BulkActionFailure> Failures);
