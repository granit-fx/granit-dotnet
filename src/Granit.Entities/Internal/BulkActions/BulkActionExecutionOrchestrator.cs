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
        var bulkExecutor = _serviceProvider.GetService(bulkExecutorType)
            ?? throw new InvalidOperationException(
                $"Bulk executor '{bulkExecutorType.Name}' is not registered in the DI container.");

        var method = bulkExecutorType.GetMethod(
            nameof(IBulkActionExecutor<TEntity>.ExecuteBulkAsync),
            new[] { typeof(IReadOnlyList<TEntity>), typeof(JsonElement), typeof(CancellationToken) });

        if (method is null)
        {
            throw new InvalidOperationException(
                $"Bulk executor '{bulkExecutorType.Name}' does not have an ExecuteBulkAsync method.");
        }

        var result = await (Task<BulkActionResult>)method.Invoke(bulkExecutor, new object[] { entities, payload, cancellationToken })!;
        return result;
    }

    private async Task<BulkActionResult> ExecutePerRowAsync<TEntity>(
        Type executorType,
        IReadOnlyList<TEntity> entities,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var executor = _serviceProvider.GetService(executorType)
            ?? throw new InvalidOperationException(
                $"Executor '{executorType.Name}' is not registered in the DI container.");

        var method = executorType.GetMethod(
            nameof(IEntityActionExecutor<TEntity>.ExecuteAsync),
            new[] { typeof(TEntity), typeof(JsonElement), typeof(CancellationToken) });

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
                var invokedTask = (Task)method.Invoke(executor, new object[] { entity, payload, cancellationToken })!;
                await invokedTask.ConfigureAwait(false);

                var resultProperty = invokedTask.GetType().GetProperty("Result");
                ActionResult? result = (ActionResult?)resultProperty?.GetValue(invokedTask);

                if (result is not null && result.IsSuccess)
                {
                    affectedCount++;
                }
                else if (result is not null && result.ErrorMessage is not null)
                {
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

#pragma warning restore IDE0007, IDE0008
