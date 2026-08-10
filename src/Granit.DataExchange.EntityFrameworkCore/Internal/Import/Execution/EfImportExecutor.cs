using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Reporting;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Execution;

/// <summary>
/// EF Core implementation of <see cref="IImportExecutor{TEntity}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Owns a single <typeparamref name="TContext"/> and one transaction for the whole run. Rows are
/// buffered into batches of <see cref="ImportExecutionOptions.BatchSize"/>; for each batch,
/// existing rows for <see cref="RecordOperation.Update"/>/<see cref="RecordOperation.Upsert"/>
/// identities are prefetched in one query per key kind and TRACKED (never <c>AsNoTracking</c>) so
/// <c>CurrentValues.SetValues</c> actually schedules an UPDATE — the historical bug this rewrite
/// fixes was applying <c>SetValues</c> to an entity tracked by a resolver's own, already-disposed
/// context, which silently no-oped.
/// </para>
/// <para>
/// Each batch is saved inside its own savepoint. A <see cref="DbUpdateException"/> under
/// <see cref="ImportErrorBehavior.FailFast"/> propagates to the caller (the outer catch rolls back
/// the whole transaction and rethrows); otherwise the batch is rolled back to its savepoint and
/// replayed row-by-row, each in its own savepoint, to isolate the poison row(s) with an exact
/// per-row <see cref="ImportRowErrorKind.Persistence"/> error while the rest of the batch persists.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type.</typeparam>
internal sealed partial class EfImportExecutor<TEntity, TContext> : IImportExecutor<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    private const string PersistenceErrorCode = "Granit:DataExchange:PersistenceError";
    private const string UnexpectedOutcomeErrorCode = "Granit:DataExchange:Conversion:Unexpected";

    private readonly IDbContextFactory<TContext> _contextFactory;
    private readonly IDbContextFactory<DataExchangeDbContext> _mappingContextFactory;
    private readonly ImportDefinition<TEntity> _definition;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<EfImportExecutor<TEntity, TContext>> _logger;
    private readonly PropertyInfo[] _businessKeyProperties;
    private readonly HashSet<string> _excludedOnUpdateProperties;

    public EfImportExecutor(
        IDbContextFactory<TContext> contextFactory,
        IDbContextFactory<DataExchangeDbContext> mappingContextFactory,
        ImportDefinition<TEntity> definition,
        ICurrentTenant currentTenant,
        IClock clock,
        IGuidGenerator guidGenerator,
        ILogger<EfImportExecutor<TEntity, TContext>> logger)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(mappingContextFactory);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(guidGenerator);
        ArgumentNullException.ThrowIfNull(logger);

        _contextFactory = contextFactory;
        _mappingContextFactory = mappingContextFactory;
        _definition = definition;
        _currentTenant = currentTenant;
        _clock = clock;
        _guidGenerator = guidGenerator;
        _logger = logger;

        _businessKeyProperties = [.. definition.GetBusinessKeyProperties().Select(ResolveBusinessKeyProperty)];
        _excludedOnUpdateProperties = new HashSet<string>(definition.GetExcludedOnUpdateProperties(), StringComparer.Ordinal);
    }

    /// <inheritdoc/>
    public async Task<ImportReport> ExecuteAsync(
        IAsyncEnumerable<RowOutcome<TEntity>> rows,
        ImportExecutionOptions options,
        IProgress<ImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        ExecutionState state = new();

        await using TContext context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PropertyInfo> primaryKeyProperties = ResolvePrimaryKeyProperties(context);

        try
        {
            int batchSize = Math.Max(1, options.BatchSize);
            List<RowOutcome<TEntity>> batch = new(batchSize);
            int batchNumber = 0;

            await foreach (RowOutcome<TEntity> row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                state.TotalRows++;
                batch.Add(row);

                if (batch.Count >= batchSize)
                {
                    batchNumber++;
                    await ProcessBatchAsync(context, transaction, batch, batchNumber, primaryKeyProperties, options, state, cancellationToken)
                        .ConfigureAwait(false);
                    batch.Clear();
                    progress?.Report(new ImportProgress(state.TotalRows, 0, state.SucceededRows, state.FailedRows));
                }
            }

            if (batch.Count > 0)
            {
                batchNumber++;
                await ProcessBatchAsync(context, transaction, batch, batchNumber, primaryKeyProperties, options, state, cancellationToken)
                    .ConfigureAwait(false);
            }

            await CommitOrRollbackAsync(transaction, options.DryRun, cancellationToken).ConfigureAwait(false);

            if (!options.DryRun && state.PendingExternalIdInserts.Count > 0)
            {
                await PersistExternalIdMappingsAsync(state.PendingExternalIdInserts, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Roll back and rethrow: the orchestrator folds unexpected exceptions into a
            // failed report and logs them — swallowing here produced false-success reports.
            // This also catches DbUpdateException raised under ErrorBehavior.FailFast, which
            // ProcessBatchAsync deliberately lets propagate instead of isolating.
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        stopwatch.Stop();
        progress?.Report(new ImportProgress(state.TotalRows, state.TotalRows, state.SucceededRows, state.FailedRows));

        return new ImportReport
        {
            TotalRows = state.TotalRows,
            SucceededRows = state.SucceededRows,
            FailedRows = state.FailedRows,
            SkippedRows = state.SkippedRows,
            InsertedRows = state.InsertedRows,
            UpdatedRows = state.UpdatedRows,
            Duration = stopwatch.Elapsed,
            FinalStatus = DetermineFinalStatus(state.FailedRows, state.SucceededRows),
            RowErrors = state.Errors.AsReadOnly(),
        };
    }

    private async Task ProcessBatchAsync(
        TContext context,
        IDbContextTransaction transaction,
        List<RowOutcome<TEntity>> batch,
        int batchNumber,
        IReadOnlyList<PropertyInfo> primaryKeyProperties,
        ImportExecutionOptions options,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        List<PendingRow> pendingRows = ClassifyBatch(batch, state);
        await StageLookupRowsAsync(context, pendingRows, primaryKeyProperties, state, cancellationToken).ConfigureAwait(false);

        if (pendingRows.Count == 0)
        {
            return;
        }

        string savepointName = $"b{batchNumber}";
        await transaction.CreateSavepointAsync(savepointName, cancellationToken).ConfigureAwait(false);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            foreach (PendingRow pending in pendingRows)
            {
                RecordSuccess(state, pending, primaryKeyProperties);
            }
        }
        catch (DbUpdateException) when (options.ErrorBehavior != ImportErrorBehavior.FailFast)
        {
            await transaction.RollbackToSavepointAsync(savepointName, cancellationToken).ConfigureAwait(false);
            DetachBatch(context, pendingRows);
            await ReplayRowByRowAsync(context, transaction, pendingRows, batchNumber, state, primaryKeyProperties, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Splits a raw batch into accounted-for rows (Failed, Skipped, identity-Skip, identity-Ambiguous)
    /// and rows that still need a persistence decision (Insert directly, or Update/Upsert pending lookup).
    /// </summary>
    private static List<PendingRow> ClassifyBatch(List<RowOutcome<TEntity>> batch, ExecutionState state)
    {
        List<PendingRow> pendingRows = new(batch.Count);

        foreach (RowOutcome<TEntity> row in batch)
        {
            PendingRow? pendingRow = ClassifyRow(row, state);
            if (pendingRow is not null)
            {
                pendingRows.Add(pendingRow);
            }
        }

        return pendingRows;
    }

    /// <summary>
    /// Classifies a single row: returns the <see cref="PendingRow"/> that still needs a
    /// persistence decision, or <see langword="null"/> once the row has been accounted for on
    /// <paramref name="state"/> as skipped or failed.
    /// </summary>
    private static PendingRow? ClassifyRow(RowOutcome<TEntity> row, ExecutionState state)
    {
        if (row.IsSkipped)
        {
            state.SkippedRows++;
            return null;
        }

        if (row.Error is not null || row.Entity is null)
        {
            state.FailedRows++;
            state.Errors.Add(row.Error ?? new ImportRowError(
                row.RowNumber, ImportRowErrorKind.Conversion,
                [UnexpectedOutcomeErrorCode],
                "Row outcome carried neither an entity nor an error."));
            return null;
        }

        TEntity entity = row.Entity;
        RecordIdentity identity = row.Identity ?? RecordIdentity.Insert();

        if (identity.Operation == RecordOperation.Skip)
        {
            state.SkippedRows++;
            return null;
        }

        if (identity.Operation == RecordOperation.Ambiguous)
        {
            state.FailedRows++;
            state.Errors.Add(new ImportRowError(
                row.RowNumber, ImportRowErrorKind.Identity,
                identity.ReasonCodes.Count > 0 ? identity.ReasonCodes : [IdentityReasonCodes.AmbiguousMatch],
                "Identity could not be resolved unambiguously."));
            return null;
        }

        if (identity.Operation == RecordOperation.Insert)
        {
            return PendingRow.ForInsert(row, entity, identity.ExternalId);
        }

        // Update or Upsert — both require a key to look existing rows up by.
        if (identity.Key is null)
        {
            state.FailedRows++;
            state.Errors.Add(new ImportRowError(
                row.RowNumber, ImportRowErrorKind.Identity,
                [IdentityReasonCodes.AmbiguousMatch],
                $"Identity operation '{identity.Operation}' carried no key."));
            return null;
        }

        return PendingRow.ForLookup(row, entity, identity.Key.Value, identity.KeyKind, identity.Operation);
    }

    /// <summary>
    /// Adds Insert rows straight away, prefetches TRACKED existing rows for every Update/Upsert row
    /// in one query per key kind, then applies incoming values onto the tracked entity
    /// (Update/Upsert-found) or adds the incoming entity as a fresh insert (Upsert-not-found).
    /// Strict Update-not-found rows fail with <see cref="IdentityReasonCodes.MissingTarget"/> and
    /// are removed from <paramref name="pendingRows"/> — they were never staged for save.
    /// </summary>
    private async Task StageLookupRowsAsync(
        TContext context,
        List<PendingRow> pendingRows,
        IReadOnlyList<PropertyInfo> primaryKeyProperties,
        ExecutionState state,
        CancellationToken cancellationToken)
    {
        foreach (PendingRow insertRow in pendingRows.Where(static p => p.Kind == PendingRowKind.Insert))
        {
            context.Set<TEntity>().Add(insertRow.Entity);
        }

        List<PendingRow> lookupRows = [.. pendingRows.Where(static p => p.Kind == PendingRowKind.Lookup)];
        if (lookupRows.Count == 0)
        {
            return;
        }

        Dictionary<EntityKey, TEntity> trackedByPrimaryKey = await PrefetchAsync(
            context, lookupRows, EntityKeyKind.PrimaryKey, primaryKeyProperties, cancellationToken).ConfigureAwait(false);
        Dictionary<EntityKey, TEntity> trackedByBusinessKey = await PrefetchAsync(
            context, lookupRows, EntityKeyKind.BusinessKey, _businessKeyProperties, cancellationToken).ConfigureAwait(false);

        foreach (PendingRow pending in lookupRows)
        {
            Dictionary<EntityKey, TEntity> map = pending.KeyKind == EntityKeyKind.PrimaryKey ? trackedByPrimaryKey : trackedByBusinessKey;

            if (map.TryGetValue(pending.Key!.Value, out TEntity? tracked))
            {
                ApplyUpdateValues(context.Entry(tracked), pending.Entity);
                pending.Kind = PendingRowKind.Update;
                pending.TrackedEntity = tracked;
                pending.PrimaryKeyValues = ReadValues(tracked, primaryKeyProperties);
            }
            else if (pending.Operation == RecordOperation.Upsert)
            {
                context.Set<TEntity>().Add(pending.Entity);
                pending.Kind = PendingRowKind.Insert;
            }
            else
            {
                state.FailedRows++;
                state.Errors.Add(new ImportRowError(
                    pending.Row.RowNumber, ImportRowErrorKind.Identity,
                    [IdentityReasonCodes.MissingTarget],
                    "No existing record matched the update key."));
                pendingRows.Remove(pending);
            }
        }
    }

    private static async Task<Dictionary<EntityKey, TEntity>> PrefetchAsync(
        TContext context,
        List<PendingRow> lookupRows,
        EntityKeyKind kind,
        IReadOnlyList<PropertyInfo> keyProperties,
        CancellationToken cancellationToken)
    {
        Dictionary<EntityKey, TEntity> tracked = [];

        if (keyProperties.Count == 0)
        {
            return tracked;
        }

        List<EntityKey> keys = [.. lookupRows.Where(p => p.KeyKind == kind).Select(static p => p.Key!.Value).Distinct()];
        if (keys.Count == 0)
        {
            return tracked;
        }

        Expression<Func<TEntity, bool>> predicate = BuildKeyPredicate(keyProperties, keys);
        List<TEntity> found = await context.Set<TEntity>().Where(predicate).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (TEntity entity in found)
        {
            tracked[new EntityKey(ReadValues(entity, keyProperties))] = entity;
        }

        return tracked;
    }

    private async Task ReplayRowByRowAsync(
        TContext context,
        IDbContextTransaction transaction,
        List<PendingRow> pendingRows,
        int batchNumber,
        ExecutionState state,
        IReadOnlyList<PropertyInfo> primaryKeyProperties,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < pendingRows.Count; i++)
        {
            PendingRow pending = pendingRows[i];
            string savepointName = $"b{batchNumber}r{i}";
            await transaction.CreateSavepointAsync(savepointName, cancellationToken).ConfigureAwait(false);

            TEntity? tracked = null;

            if (pending.Kind == PendingRowKind.Insert)
            {
                context.Set<TEntity>().Add(pending.Entity);
            }
            else
            {
                tracked = await context.Set<TEntity>().FindAsync(pending.PrimaryKeyValues, cancellationToken).ConfigureAwait(false);
                if (tracked is null)
                {
                    await transaction.RollbackToSavepointAsync(savepointName, cancellationToken).ConfigureAwait(false);
                    RecordFailure(state, pending, "Target record disappeared before it could be updated.");
                    continue;
                }

                ApplyUpdateValues(context.Entry(tracked), pending.Entity);
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                RecordSuccess(state, pending, primaryKeyProperties);
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackToSavepointAsync(savepointName, cancellationToken).ConfigureAwait(false);
                context.Entry(pending.Kind == PendingRowKind.Insert ? pending.Entity : tracked!).State = EntityState.Detached;
                RecordFailure(state, pending, ex.InnerException?.Message ?? ex.Message);
            }
        }
    }

    private void ApplyUpdateValues(EntityEntry entry, TEntity incoming)
    {
        // EF Core rejects any attempt to change a tracked entity's key value outright — even
        // transiently mid-SetValues — so the incoming (mapped) entity's key must already match
        // the tracked entity's before SetValues runs, not be excluded afterwards like every
        // other guarded property.
        foreach (IProperty keyProperty in entry.Metadata.FindPrimaryKey()?.Properties ?? [])
        {
            if (keyProperty.PropertyInfo is { } propertyInfo)
            {
                propertyInfo.SetValue(incoming, entry.Property(keyProperty.Name).CurrentValue);
            }
        }

        entry.CurrentValues.SetValues(incoming);

        foreach (IProperty property in entry.Metadata.GetProperties())
        {
            if (!property.IsConcurrencyToken && !_excludedOnUpdateProperties.Contains(property.Name))
            {
                continue;
            }

            PropertyEntry propertyEntry = entry.Property(property.Name);
            propertyEntry.CurrentValue = propertyEntry.OriginalValue;
        }
    }

    private static void RecordSuccess(ExecutionState state, PendingRow pending, IReadOnlyList<PropertyInfo> primaryKeyProperties)
    {
        state.SucceededRows++;

        if (pending.Kind == PendingRowKind.Insert)
        {
            state.InsertedRows++;

            if (pending.ExternalId is not null)
            {
                Guid? internalId = GetPrimaryKeyGuid(pending.Entity, primaryKeyProperties);
                if (internalId is not null)
                {
                    state.PendingExternalIdInserts.Add((pending.ExternalId, internalId.Value));
                }
            }
        }
        else
        {
            state.UpdatedRows++;
        }
    }

    private static void RecordFailure(ExecutionState state, PendingRow pending, string message)
    {
        state.FailedRows++;
        state.Errors.Add(new ImportRowError(
            pending.Row.RowNumber, ImportRowErrorKind.Persistence, [PersistenceErrorCode], message));
    }

    private static void DetachBatch(TContext context, List<PendingRow> pendingRows)
    {
        foreach (PendingRow pending in pendingRows)
        {
            TEntity tracked = pending.Kind == PendingRowKind.Update ? pending.TrackedEntity! : pending.Entity;
            EntityEntry entry = context.Entry(tracked);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task PersistExternalIdMappingsAsync(
        List<(string ExternalId, Guid InternalId)> pendingInserts, CancellationToken cancellationToken)
    {
        // Deduplicate defensively — two rows resolving to the same never-before-seen external ID
        // within one run would otherwise violate the mapping table's unique index.
        Dictionary<string, Guid> distinct = new(StringComparer.Ordinal);
        foreach ((string externalId, Guid internalId) in pendingInserts)
        {
            distinct[externalId] = internalId;
        }

        Guid? tenantId = _currentTenant.IsAvailable ? _currentTenant.Id : null;
        string definitionName = _definition.Name;
        DateTimeOffset now = _clock.Now;

        await using DataExchangeDbContext mappingContext =
            await _mappingContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        foreach ((string externalId, Guid internalId) in distinct)
        {
            mappingContext.ExternalIdMappings.Add(new ExternalIdMappingEntity
            {
                Id = _guidGenerator.Create(),
                DefinitionName = definitionName,
                ExternalId = externalId,
                InternalId = internalId,
                TenantId = tenantId,
                CreatedAt = now,
            });
        }

        try
        {
            await mappingContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // The unique index (DefinitionName, ExternalId, TenantId) makes this write idempotent
            // in intent, but the app-context commit and this mapping write are two separate
            // transactions — a crash between them leaves the mapping unwritten, and a subsequent
            // retry of the same file will insert a new application row rather than update the
            // orphaned one. Documented limitation: closing it needs a single distributed
            // transaction across DataExchangeDbContext and the application DbContext.
            LogExternalIdMappingWriteFailed(ex, definitionName);
        }
    }

    private static PropertyInfo ResolveBusinessKeyProperty(string propertyName) =>
        typeof(TEntity).GetProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"Business key property '{propertyName}' does not exist on entity type '{typeof(TEntity).Name}'.");

    private static IReadOnlyList<PropertyInfo> ResolvePrimaryKeyProperties(TContext context)
    {
        IKey? primaryKey = context.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey();
        if (primaryKey is null)
        {
            return [];
        }

        return [.. primaryKey.Properties.Select(static p => p.PropertyInfo).OfType<PropertyInfo>()];
    }

    private static object?[] ReadValues(TEntity entity, IReadOnlyList<PropertyInfo> properties) =>
        [.. properties.Select(p => p.GetValue(entity))];

    private static Guid? GetPrimaryKeyGuid(TEntity entity, IReadOnlyList<PropertyInfo> primaryKeyProperties) =>
        primaryKeyProperties is [PropertyInfo single] && single.GetValue(entity) is Guid guid ? guid : null;

    private static Expression<Func<TEntity, bool>> BuildKeyPredicate(
        IReadOnlyList<PropertyInfo> keyProperties, IReadOnlyList<EntityKey> keys)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression? combined = null;

        foreach (EntityKey key in keys)
        {
            Expression? rowPredicate = null;

            for (int i = 0; i < keyProperties.Count; i++)
            {
                MemberExpression property = Expression.Property(parameter, keyProperties[i]);
                ConstantExpression constant = Expression.Constant(key.Components[i], property.Type);
                BinaryExpression equality = Expression.Equal(property, constant);
                rowPredicate = rowPredicate is null ? equality : Expression.AndAlso(rowPredicate, equality);
            }

            combined = combined is null ? rowPredicate : Expression.OrElse(combined, rowPredicate!);
        }

        return Expression.Lambda<Func<TEntity, bool>>(combined ?? Expression.Constant(false), parameter);
    }

    private static async Task CommitOrRollbackAsync(
        IDbContextTransaction transaction, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static ImportJobStatus DetermineFinalStatus(int failedRows, int succeededRows)
    {
        if (failedRows == 0)
        {
            return ImportJobStatus.Completed;
        }

        return succeededRows > 0
            ? ImportJobStatus.PartiallyCompleted
            : ImportJobStatus.Failed;
    }

    [LoggerMessage(1, LogLevel.Warning,
        "Failed to persist external-ID mappings for import definition '{DefinitionName}' after the application data committed.")]
    private partial void LogExternalIdMappingWriteFailed(Exception exception, string definitionName);

    private enum PendingRowKind
    {
        Insert,
        Lookup,
        Update,
    }

    private sealed class PendingRow
    {
        public required RowOutcome<TEntity> Row { get; init; }
        public required TEntity Entity { get; init; }
        public required PendingRowKind Kind { get; set; }
        public string? ExternalId { get; init; }
        public EntityKey? Key { get; init; }
        public EntityKeyKind KeyKind { get; init; }
        public RecordOperation Operation { get; init; }
        public TEntity? TrackedEntity { get; set; }
        public object?[]? PrimaryKeyValues { get; set; }

        public static PendingRow ForInsert(RowOutcome<TEntity> row, TEntity entity, string? externalId) =>
            new() { Row = row, Entity = entity, Kind = PendingRowKind.Insert, ExternalId = externalId };

        public static PendingRow ForLookup(
            RowOutcome<TEntity> row, TEntity entity, EntityKey key, EntityKeyKind keyKind, RecordOperation operation) =>
            new() { Row = row, Entity = entity, Kind = PendingRowKind.Lookup, Key = key, KeyKind = keyKind, Operation = operation };
    }

    private sealed class ExecutionState
    {
        public int TotalRows { get; set; }
        public int SucceededRows { get; set; }
        public int FailedRows { get; set; }
        public int SkippedRows { get; set; }
        public int InsertedRows { get; set; }
        public int UpdatedRows { get; set; }
        public List<ImportRowError> Errors { get; } = [];
        public List<(string ExternalId, Guid InternalId)> PendingExternalIdInserts { get; } = [];
    }
}
