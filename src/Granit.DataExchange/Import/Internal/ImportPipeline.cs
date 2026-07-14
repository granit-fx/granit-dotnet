using System.Reflection;
using System.Runtime.CompilerServices;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Mapping.Internal;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.DataExchange.Import.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Typed <see cref="IImportPipeline"/>: Parse → Map → Validate → Resolve Identity (batched) → Execute,
/// with errors carried as <see cref="RowOutcome{TEntity}"/> data — nothing is silently dropped.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
internal sealed class ImportPipeline<TEntity>(
    ImportDefinition<TEntity> definition,
    Lazy<CompiledDataMapper<TEntity>> compiledMapper,
    IServiceProvider scopedProvider) : IImportPipeline
    where TEntity : class
{
    private const string IdentityResolutionErrorCode = "Granit:DataExchange:Identity:ResolutionFailed";

    /// <inheritdoc/>
    public string DefinitionName => definition.Name;

    /// <inheritdoc/>
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc/>
    public async Task<ImportReport> ExecuteAsync(ImportPipelineContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        IFileParser parser = ResolveParser(context.MimeType);
        IImportExecutor<TEntity> executor = scopedProvider.GetService<IImportExecutor<TEntity>>()
            ?? throw new InvalidOperationException(
                $"No IImportExecutor<{typeof(TEntity).Name}> is registered for import definition '{definition.Name}'. " +
                $"Register one with services.AddImportExecutor<{typeof(TEntity).Name}, TContext>() " +
                "from Granit.DataExchange.EntityFrameworkCore, or provide a custom implementation.");

        // A DI-registered mapper overrides the definition-compiled default.
        IDataMapper<TEntity> mapper = scopedProvider.GetService<IDataMapper<TEntity>>() ?? compiledMapper.Value;
        IRowValidator<TEntity>? validator = scopedProvider.GetService<IRowValidator<TEntity>>();
        IRecordIdentityResolver<TEntity>? identityResolver = scopedProvider.GetService<IRecordIdentityResolver<TEntity>>();
        ImportOptions importOptions = scopedProvider.GetRequiredService<IOptions<ImportOptions>>().Value;
        PropertyInfo[] businessKeyProperties = [.. definition.GetBusinessKeyProperties()
            .Select(name => typeof(TEntity).GetProperty(name))
            .Where(property => property is not null)!];

        IAsyncEnumerable<RowOutcome<TEntity>> rows = BuildOutcomesAsync(
            parser, mapper, validator, identityResolver, businessKeyProperties, importOptions, context, cancellationToken);

        return await executor.ExecuteAsync(rows, context.ExecutionOptions, context.Progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private IFileParser ResolveParser(string mimeType)
    {
        IEnumerable<IFileParser> parsers = scopedProvider.GetServices<IFileParser>();
        IFileParser? parser = parsers.FirstOrDefault(p => p.CanParse(mimeType));
        if (parser is not null)
        {
            return parser;
        }

        string registered = parsers.Any()
            ? string.Join(", ", parsers.Select(p => p.GetType().Name))
            : "none";
        throw new InvalidOperationException(
            $"No IFileParser registered for MIME type '{mimeType}'. " +
            $"Registered parsers: [{registered}]. " +
            "Ensure the corresponding module is added: GranitDataExchangeCsvModule for CSV, GranitDataExchangeExcelModule for Excel.");
    }

    /// <summary>
    /// Parses, maps, and validates every row, then resolves identity in chunks of
    /// <see cref="ImportExecutionOptions.BatchSize"/> — one resolver call per chunk instead of
    /// one per row — while preserving the original row order in the output stream.
    /// </summary>
    private static async IAsyncEnumerable<RowOutcome<TEntity>> BuildOutcomesAsync(
        IFileParser parser,
        IDataMapper<TEntity> mapper,
        IRowValidator<TEntity>? validator,
        IRecordIdentityResolver<TEntity>? identityResolver,
        PropertyInfo[] businessKeyProperties,
        ImportOptions importOptions,
        ImportPipelineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        FileParsingOptions parsingOptions = new() { MimeType = context.MimeType };
        int batchSize = Math.Max(1, context.ExecutionOptions.BatchSize);
        List<RowOutcome<TEntity>> buffer = new(batchSize);

        await foreach (RawImportRow row in parser.ParseAsync(context.FileStream, parsingOptions, cancellationToken)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            buffer.Add(await MapAndValidateRowAsync(row, mapper, validator, context.Mappings, importOptions, cancellationToken).ConfigureAwait(false));

            if (buffer.Count >= batchSize)
            {
                foreach (RowOutcome<TEntity> outcome in await ResolveIdentityChunkAsync(
                    buffer, identityResolver, businessKeyProperties, cancellationToken).ConfigureAwait(false))
                {
                    yield return outcome;
                }

                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            foreach (RowOutcome<TEntity> outcome in await ResolveIdentityChunkAsync(
                buffer, identityResolver, businessKeyProperties, cancellationToken).ConfigureAwait(false))
            {
                yield return outcome;
            }
        }
    }

    /// <summary>
    /// Stamps identity onto the <c>Ok</c> outcomes of one chunk (in place, preserving order).
    /// Failed/Skipped outcomes are never sent to the resolver.
    /// </summary>
    private static async Task<List<RowOutcome<TEntity>>> ResolveIdentityChunkAsync(
        List<RowOutcome<TEntity>> chunk,
        IRecordIdentityResolver<TEntity>? identityResolver,
        PropertyInfo[] businessKeyProperties,
        CancellationToken cancellationToken)
    {
        List<int> okIndexes = [];
        List<TEntity> okEntities = [];

        for (int i = 0; i < chunk.Count; i++)
        {
            if (chunk[i].Entity is TEntity entity)
            {
                okIndexes.Add(i);
                okEntities.Add(entity);
            }
        }

        if (okEntities.Count == 0)
        {
            return chunk;
        }

        IReadOnlyList<RecordIdentity>? identities = null;
        string? resolverErrorMessage = null;

        if (identityResolver is not null)
        {
            try
            {
                identities = await identityResolver.ResolveBatchAsync(okEntities, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resolverErrorMessage = ex.Message;
            }
        }
        else
        {
            identities = DefaultIdentities(okEntities, businessKeyProperties);
        }

        for (int i = 0; i < okIndexes.Count; i++)
        {
            int index = okIndexes[i];

            chunk[index] = resolverErrorMessage is not null
                ? RowOutcome<TEntity>.Failed(chunk[index].RowNumber, new ImportRowError(
                    chunk[index].RowNumber,
                    ImportRowErrorKind.Identity,
                    [IdentityResolutionErrorCode],
                    resolverErrorMessage))
                : chunk[index] with { Identity = identities![i] };
        }

        return chunk;
    }

    /// <summary>
    /// Default identity assignment when no <see cref="IRecordIdentityResolver{TEntity}"/> is
    /// registered: business keys declared on the definition → <see cref="RecordOperation.Upsert"/>
    /// keyed on those properties; otherwise plain <see cref="RecordOperation.Insert"/>.
    /// </summary>
    private static List<RecordIdentity> DefaultIdentities(
        List<TEntity> entities, PropertyInfo[] businessKeyProperties)
    {
        if (businessKeyProperties.Length == 0)
        {
            return [.. entities.Select(static _ => RecordIdentity.Insert())];
        }

        List<RecordIdentity> results = new(entities.Count);
        foreach (TEntity entity in entities)
        {
            object?[] components = new object?[businessKeyProperties.Length];
            bool missing = false;

            for (int i = 0; i < businessKeyProperties.Length; i++)
            {
                object? value = businessKeyProperties[i].GetValue(entity);
                if (value is null || (value is string text && string.IsNullOrEmpty(text)))
                {
                    missing = true;
                }

                components[i] = value;
            }

            results.Add(missing
                ? RecordIdentity.Ambiguous(IdentityReasonCodes.MissingKeyComponent)
                : RecordIdentity.Upsert(new EntityKey(components), EntityKeyKind.BusinessKey));
        }

        return results;
    }

    private static async Task<RowOutcome<TEntity>> MapAndValidateRowAsync(
        RawImportRow row,
        IDataMapper<TEntity> mapper,
        IRowValidator<TEntity>? validator,
        IReadOnlyList<ImportColumnMapping> mappings,
        ImportOptions importOptions,
        CancellationToken cancellationToken)
    {
        // Map — conversion failures become Failed outcomes, all-empty rows become Skipped.
        MappingResult<TEntity> mappingResult = await mapper
            .MapAsync(row, mappings, importOptions, cancellationToken).ConfigureAwait(false);

        if (mappingResult.Errors.Count > 0)
        {
            return RowOutcome<TEntity>.Failed(row.RowNumber, new ImportRowError(
                row.RowNumber,
                ImportRowErrorKind.Conversion,
                [.. mappingResult.Errors.Select(e => e.ErrorCode).Distinct(StringComparer.Ordinal)],
                string.Join("; ", mappingResult.Errors.Select(FormatConversionError))));
        }

        if (mappingResult.Entity is null)
        {
            // Empty-row signal from the mapper: every mapped cell blank.
            return RowOutcome<TEntity>.Skipped(row.RowNumber);
        }

        TEntity entity = mappingResult.Entity;

        // Validate
        if (validator is not null)
        {
            RowValidationResult validationResult = await validator
                .ValidateAsync(entity, row.RowNumber, cancellationToken).ConfigureAwait(false);
            if (!validationResult.IsValid)
            {
                return RowOutcome<TEntity>.Failed(row.RowNumber, new ImportRowError(
                    row.RowNumber,
                    ImportRowErrorKind.Validation,
                    [.. validationResult.Errors.Select(e => e.ErrorCode).Distinct(StringComparer.Ordinal)],
                    string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))));
            }
        }

        return RowOutcome<TEntity>.Ok(row.RowNumber, entity);
    }

    private static string FormatConversionError(CellConversionError error) =>
        $"Column '{error.SourceColumn}' → {error.TargetProperty}: {error.ErrorCode} (value: '{error.RawValue}')";
}
