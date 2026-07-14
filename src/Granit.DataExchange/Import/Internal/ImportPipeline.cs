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
/// Typed <see cref="IImportPipeline"/>: Parse → Map → Validate → Resolve Identity → Execute,
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

        IAsyncEnumerable<RowOutcome<TEntity>> rows = BuildOutcomesAsync(
            parser, mapper, validator, identityResolver, importOptions, context, cancellationToken);

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

    private static async IAsyncEnumerable<RowOutcome<TEntity>> BuildOutcomesAsync(
        IFileParser parser,
        IDataMapper<TEntity> mapper,
        IRowValidator<TEntity>? validator,
        IRecordIdentityResolver<TEntity>? identityResolver,
        ImportOptions importOptions,
        ImportPipelineContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        FileParsingOptions parsingOptions = new() { MimeType = context.MimeType };

        await foreach (RawImportRow row in parser.ParseAsync(context.FileStream, parsingOptions, cancellationToken)
            .WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return await ProcessRowAsync(row, mapper, validator, identityResolver, importOptions, context, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task<RowOutcome<TEntity>> ProcessRowAsync(
        RawImportRow row,
        IDataMapper<TEntity> mapper,
        IRowValidator<TEntity>? validator,
        IRecordIdentityResolver<TEntity>? identityResolver,
        ImportOptions importOptions,
        ImportPipelineContext context,
        CancellationToken cancellationToken)
    {
        // Map — conversion failures become Failed outcomes, all-empty rows become Skipped.
        MappingResult<TEntity> mappingResult = await mapper
            .MapAsync(row, context.Mappings, importOptions, cancellationToken).ConfigureAwait(false);

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

        // Resolve identity — resolver exceptions are row-level failures, not job killers.
        RecordIdentity<TEntity>? identity = null;
        if (identityResolver is not null)
        {
            try
            {
                identity = await identityResolver.ResolveAsync(entity, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return RowOutcome<TEntity>.Failed(row.RowNumber, new ImportRowError(
                    row.RowNumber,
                    ImportRowErrorKind.Identity,
                    [IdentityResolutionErrorCode],
                    ex.Message));
            }
        }

        return RowOutcome<TEntity>.Ok(row.RowNumber, entity, identity);
    }

    private static string FormatConversionError(CellConversionError error) =>
        $"Column '{error.SourceColumn}' → {error.TargetProperty}: {error.ErrorCode} (value: '{error.RawValue}')";
}
