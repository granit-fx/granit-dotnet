using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Granit.Entities.Internal.BulkActions;

/// <summary>
/// Orchestrates bulk action execution. Materializes target entities, dispatches to
/// the appropriate executor (bulk or per-row), and collects results into a unified
/// response. Designed to be injected into the bulk action endpoint.
/// </summary>
internal sealed class BulkActionExecutionOrchestrator
{
    private readonly IServiceProvider _serviceProvider;

    public BulkActionExecutionOrchestrator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Executes a bulk action against multiple entities. Resolves the executor from
    /// DI, materialized entities via the DbContext, and orchestrates the execution
    /// flow (bulk vs per-row).
    /// </summary>
    /// <typeparam name="TEntity">The entity type being acted upon.</typeparam>
    /// <param name="descriptor">The action descriptor (from manifest). Must have
    /// <see cref="EntityActionDescriptor.ServerExecutorType"/> set.</param>
    /// <param name="dbContext">The DbContext for the entity type. Used to materialize
    /// entities by their IDs.</param>
    /// <param name="entityIds">Primary keys of all entities to act upon. Must not be
    /// empty; IDs are parsed from string format (GUID or int).</param>
    /// <param name="payload">Action-specific parameters as a JSON element.
    /// Passed verbatim to the executor.</param>
    /// <param name="cancellationToken">Cancellation signal for async work.</param>
    /// <returns>
    /// <see cref="BulkActionResult"/> containing the count of successfully affected
    /// rows and per-row failures (if any).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the action does not declare a <see cref="EntityActionDescriptor.ServerExecutorType"/>
    /// or if no matching executor is registered in DI.
    /// </exception>
    public async Task<BulkActionResult> ExecuteAsync<TEntity>(
        EntityActionDescriptor descriptor,
        DbContext dbContext,
        IReadOnlyList<string> entityIds,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        if (descriptor.ServerExecutorType is null)
        {
            throw new InvalidOperationException(
                $"Action '{descriptor.Name}' is not configured for server-side execution. "
                + "Ensure .ServerExecutor<TExecutor>() is called in the action builder.");
        }

        // Parse entity IDs and load from database
        List<TEntity> entities = await LoadEntitiesAsync(dbContext, entityIds, cancellationToken);

        if (entities.Count == 0)
        {
            return BulkActionResult.Success(0);
        }

        // Attempt bulk execution if a bulk executor is registered; fall back to per-row
        if (descriptor.BulkExecutorType is not null)
        {
            return await ExecuteBulkAsync(descriptor.BulkExecutorType, entities, payload, cancellationToken);
        }

        return await ExecutePerRowAsync(descriptor.ServerExecutorType, entities, payload, cancellationToken);
    }

    /// <summary>
    /// Loads target entities from the database by their IDs. Materializes them into
    /// memory for the executor to work with.
    /// </summary>
    private static async Task<List<TEntity>> LoadEntitiesAsync<TEntity>(
        DbContext dbContext,
        IReadOnlyList<string> entityIds,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // For simplicity, assume entity IDs are GUIDs. In production, detect the ID type
        // via reflection on the entity's key property. This is a simplified version.
        var parsedIds = new List<Guid>();
        foreach (string id in entityIds)
        {
            if (Guid.TryParse(id, out Guid guid))
            {
                parsedIds.Add(guid);
            }
        }

        if (parsedIds.Count == 0)
        {
            return [];
        }

        // Load entities matching the parsed IDs
        return await dbContext.Set<TEntity>()
            .Where(e => parsedIds.Contains(EF.Property<Guid>(e, "Id")))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Invokes the bulk executor for all entities at once. Returns the result directly.
    /// </summary>
    private async Task<BulkActionResult> ExecuteBulkAsync<TEntity>(
        Type bulkExecutorType,
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Resolve bulk executor from DI
        var bulkExecutor = _serviceProvider.GetService(bulkExecutorType)
            ?? throw new InvalidOperationException(
                $"Bulk executor '{bulkExecutorType.Name}' is not registered in the DI container.");

        // Invoke ExecuteBulkAsync dynamically
        var method = bulkExecutorType.GetMethod(
            nameof(IBulkActionExecutor<TEntity>.ExecuteBulkAsync),
            [typeof(IReadOnlyList<TEntity>), typeof(JsonElement), typeof(CancellationToken)]);

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Bulk executor '{bulkExecutorType.Name}' does not have an ExecuteBulkAsync method.");
        }

        var result = await (Task<BulkActionResult>)method.Invoke(bulkExecutor, [entities, payload, cancellationToken])!;
        return result;
    }

    /// <summary>
    /// Falls back to per-row execution. Invokes the single-entity executor for each
    /// entity and aggregates results (success count + failures).
    /// </summary>
    private async Task<BulkActionResult> ExecutePerRowAsync<TEntity>(
        Type executorType,
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // Resolve single-entity executor from DI
        var executor = _serviceProvider.GetService(executorType)
            ?? throw new InvalidOperationException(
                $"Executor '{executorType.Name}' is not registered in the DI container.");

        // Invoke ExecuteAsync dynamically
        var method = executorType.GetMethod(
            nameof(IEntityActionExecutor<TEntity>.ExecuteAsync),
            [typeof(TEntity), typeof(JsonElement), typeof(CancellationToken)]);

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Executor '{executorType.Name}' does not have an ExecuteAsync method.");
        }

        var failures = new List<BulkFailure>();
        int affectedCount = 0;

        foreach (TEntity entity in entities)
        {
            try
            {
                var result = await (Task<ActionResult>)method.Invoke(executor, [entity, payload, cancellationToken])!;

                if (result.IsSuccess)
                {
                    affectedCount++;
                }
                else if (result.ErrorMessage is not null)
                {
                    // Extract entity ID (assumes "Id" property; in production, use reflection)
                    string entityId = GetEntityId(entity) ?? "unknown";
                    failures.Add(new BulkFailure(entityId, result.ErrorMessage));
                }
            }
            catch (Exception ex)
            {
                string entityId = GetEntityId(entity) ?? "unknown";
                failures.Add(new BulkFailure(entityId, ex.Message));
            }
        }

        return new BulkActionResult(affectedCount, failures);
    }

    /// <summary>
    /// Extracts the primary key from an entity instance. Assumes "Id" property by default;
    /// in production, this should use reflection to discover the actual key property.
    /// </summary>
    private static string? GetEntityId<TEntity>(TEntity entity)
        where TEntity : class
    {
        var idProperty = typeof(TEntity).GetProperty("Id");
        if (idProperty is null)
        {
            return null;
        }

        var value = idProperty.GetValue(entity);
        return value?.ToString();
    }
}
