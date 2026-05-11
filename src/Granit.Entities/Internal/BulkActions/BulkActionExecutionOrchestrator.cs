using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Granit.Entities.Internal.BulkActions;

#pragma warning disable IDE0007, IDE0008

/// <summary>
/// EF Core-backed orchestrator for bulk action execution. Materializes target entities,
/// dispatches to the appropriate executor (bulk or per-row), and collects results.
/// </summary>
/// <remarks>
/// <para>
/// The caller is responsible for managing the DbContext transaction lifecycle.
/// For atomic operations, wrap the orchestrator call in a transaction:
/// </para>
/// <code>
/// using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
/// try
/// {
///     var result = await orchestrator.ExecuteAsync(descriptor, dbContext, entityIds, payload, cancellationToken);
///     await transaction.CommitAsync(cancellationToken);
/// }
/// catch
/// {
///     await transaction.RollbackAsync(cancellationToken);
///     throw;
/// }
/// </code>
/// </remarks>
public sealed class BulkActionExecutionOrchestrator
{
    private readonly IServiceProvider _serviceProvider;

    public BulkActionExecutionOrchestrator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

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

        List<TEntity> entities = await LoadEntitiesAsync<TEntity>(dbContext, entityIds, cancellationToken);

        if (entities.Count == 0)
        {
            return BulkActionResult.Success(0);
        }

        if (descriptor.BulkExecutorType is not null)
        {
            return await ExecuteBulkAsync(descriptor.BulkExecutorType, entities, payload, cancellationToken);
        }

        return await ExecutePerRowAsync(descriptor.ServerExecutorType, entities, payload, cancellationToken);
    }

    private static async Task<List<TEntity>> LoadEntitiesAsync<TEntity>(
        DbContext dbContext,
        IReadOnlyList<string> entityIds,
        CancellationToken cancellationToken)
        where TEntity : class
    {
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
            return new List<TEntity>();
        }

        return await dbContext.Set<TEntity>()
            .Where(e => parsedIds.Contains(EF.Property<Guid>(e, "Id")))
            .ToListAsync(cancellationToken);
    }

    private async Task<BulkActionResult> ExecuteBulkAsync<TEntity>(
        Type bulkExecutorType,
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var bulkExecutor = _serviceProvider.GetService(bulkExecutorType);
        if (bulkExecutor is null)
        {
            throw new InvalidOperationException(
                $"Bulk executor '{bulkExecutorType.Name}' is not registered in the DI container.");
        }

        // Cast directly to the interface for type safety and performance
        var typedExecutor = (IBulkActionExecutor<TEntity>)bulkExecutor;
        return await typedExecutor.ExecuteBulkAsync(entities, payload, cancellationToken);
    }

    private async Task<BulkActionResult> ExecutePerRowAsync<TEntity>(
        Type executorType,
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var executor = _serviceProvider.GetService(executorType);
        if (executor is null)
        {
            throw new InvalidOperationException(
                $"Executor '{executorType.Name}' is not registered in the DI container.");
        }

        // Cast directly to the interface for type safety and performance
        var typedExecutor = (IEntityActionExecutor<TEntity>)executor;

        var failures = new List<BulkFailure>();
        int affectedCount = 0;

        foreach (TEntity entity in entities)
        {
            try
            {
                ActionResult result = await typedExecutor.ExecuteAsync(entity, payload, cancellationToken);

                if (result.IsSuccess)
                {
                    affectedCount++;
                }
                else if (result.ErrorMessage is not null)
                {
                    string entityId = GetEntityId(entity) ?? "unknown";
                    failures.Add(new BulkFailure(entityId, result.ErrorMessage));
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout or external cancellation — re-throw, don't swallow
                throw;
            }
            catch (Exception ex)
            {
                string entityId = GetEntityId(entity) ?? "unknown";
                // Capture business logic exceptions; system exceptions will propagate
                failures.Add(new BulkFailure(entityId, ex.Message));
            }
        }

        return new BulkActionResult(affectedCount, failures);
    }

    private static string? GetEntityId<TEntity>(TEntity entity)
        where TEntity : class
    {
        var idProperty = typeof(TEntity).GetProperty("Id");
        if (idProperty is not null)
        {
            var value = idProperty.GetValue(entity);
            return value?.ToString();
        }

        // Fallback: check for "Key" or "Guid" properties
        idProperty = typeof(TEntity).GetProperty("Key") ?? typeof(TEntity).GetProperty("Guid");
        if (idProperty is not null)
        {
            var value = idProperty.GetValue(entity);
            return value?.ToString();
        }

        return null;
    }
}

#pragma warning restore IDE0007, IDE0008
