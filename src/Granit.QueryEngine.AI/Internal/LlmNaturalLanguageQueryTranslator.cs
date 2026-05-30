using System.Diagnostics;
using System.Globalization;
using System.Text;
using Granit.AI;
using Granit.MultiTenancy;
using Granit.QueryEngine.AI.Diagnostics;
using Granit.QueryEngine.AI.Options;
using Granit.QueryEngine.Meta;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.AI.Internal;

/// <summary>
/// LLM-backed <see cref="INaturalLanguageQueryTranslator"/> built on the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064). The query schema and available
/// fields are folded into the developer-controlled instruction; the user's phrase travels as
/// untrusted, sanitized content. The model's output is whitelisted against the metadata
/// (CWE-20 / OWASP LLM02) before it becomes a <see cref="QueryRequest"/>.
/// </summary>
internal sealed partial class LlmNaturalLanguageQueryTranslator(
    IStructuredCompletion structuredCompletion,
    IOptions<QueryEngineAIOptions> options,
    ILogger<LlmNaturalLanguageQueryTranslator> logger,
    IClock clock,
    QueryEngineAIMetrics? metrics = null,
    ICurrentTenant? currentTenant = null) : INaturalLanguageQueryTranslator
{
    /// <inheritdoc/>
    public async Task<QueryRequest?> TranslateAsync(
        string naturalLanguage,
        QueryMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return null;
        }

        using Activity? activity = QueryEngineAIActivitySource.Source.StartActivity(QueryEngineAIActivitySource.Translate);
        long startTimestamp = Stopwatch.GetTimestamp();
        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;

        QueryEngineAIOptions opts = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = BuildInstruction(metadata),
            Content = naturalLanguage,
            ContentLabel = "Query",
            WorkspaceName = opts.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<LlmQueryPayload> result = await structuredCompletion
                .CompleteAsync<LlmQueryPayload>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.Status != StructuredCompletionStatus.Succeeded)
            {
                LogInvalidResponse(logger, naturalLanguage.Length, result.Status.ToString());
                metrics?.RecordTranslationFailed(tenantId, MapFailureReason(result.Status));
                return null;
            }

            metrics?.RecordTranslationExecuted(tenantId, "success");
            return ValidateAndConvert(result.Value!, metadata);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogTimeout(logger, naturalLanguage.Length);
            metrics?.RecordTranslationFailed(tenantId, "timeout");
            return null;
        }
        finally
        {
            double elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            metrics?.RecordTranslationDuration(tenantId, elapsed);
        }
    }

    private static string MapFailureReason(StructuredCompletionStatus status) => status switch
    {
        StructuredCompletionStatus.TransportFailure => "error",
        _ => "invalid_response",
    };

    internal string BuildInstruction(QueryMetadata metadata)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a query translator. Convert the user's natural-language phrase (in the data block)");
        sb.AppendLine("into the structured query format. Produce only the fields you need, omitting the rest:");
        sb.AppendLine("- page / pageSize: integers for pagination.");
        sb.AppendLine("- sort: comma-separated field names, prefix - for descending (e.g. \"-createdAt,lastName\").");
        sb.AppendLine("- filter: a list of clauses, each a \"key\" of the form \"fieldName.operator\" and a string \"value\".");
        sb.AppendLine("- quickFilters: names drawn from the available quick-filter list.");
        sb.AppendLine("- groupBy: a single field name.");
        sb.AppendLine();

        // Filterable fields
        if (metadata.FilterableFields.Count > 0)
        {
            sb.AppendLine("Available filterable fields:");

            foreach (FilterableField field in metadata.FilterableFields)
            {
                string operators = string.Join(", ", field.Operators.Select(static op => op.ToString().ToLowerInvariant()));
                sb.AppendLine($"  - {field.Name} (type: {field.Type}, operators: {operators})");
            }

            sb.AppendLine();
        }

        // Sortable fields
        if (metadata.SortableFields.Count > 0)
        {
            sb.AppendLine("Available sortable fields:");

            foreach (SortableField field in metadata.SortableFields)
            {
                sb.AppendLine($"  - {field.Name}");
            }

            sb.AppendLine("Sort format: comma-separated field names. Prefix with - for descending (e.g. \"-createdAt,lastName\").");
            sb.AppendLine();
        }

        // Quick filters
        if (metadata.QuickFilters.Count > 0)
        {
            sb.AppendLine("Available quick filters:");

            foreach (QuickFilterMeta qf in metadata.QuickFilters)
            {
                sb.AppendLine($"  - \"{qf.Name}\" (label: {qf.Label})");
            }

            sb.AppendLine();
        }

        // Date filters
        if (metadata.DateFilters.Count > 0)
        {
            sb.AppendLine("Available date filter fields:");

            foreach (DateFilterMeta df in metadata.DateFilters)
            {
                string periods = string.Join(", ", df.AvailablePeriods.Select(static p => p.ToString()));
                sb.AppendLine($"  - {df.Name} (periods: {periods})");
            }

            sb.AppendLine();
        }

        // Group by
        if (metadata.GroupByFields.Count > 0)
        {
            sb.AppendLine("Available group-by fields:");

            foreach (GroupByField field in metadata.GroupByFields)
            {
                sb.AppendLine($"  - {field.Name}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("Filter key format: \"fieldName.operator\" (e.g. \"status.eq\", \"amount.gte\", \"name.contains\").");
        sb.Append(CultureInfo.InvariantCulture, $"Current date: {clock.Now:yyyy-MM-dd}. ");
        sb.AppendLine("Use this for relative date references (\"this week\", \"last month\").");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Use only the fields and operators listed above.");
        sb.AppendLine("- Omit properties that are not needed.");
        sb.AppendLine("- For date ranges, use gte/lte operators with ISO 8601 dates.");

        return sb.ToString();
    }

    private static QueryRequest ValidateAndConvert(LlmQueryPayload dto, QueryMetadata metadata)
    {
        // Build whitelist of allowed filter keys from metadata (CWE-20, LLM02)
        var allowedFilterKeys = metadata.FilterableFields
            .SelectMany(f => f.Operators.Select(op => $"{f.Name}.{op.ToString().ToLowerInvariant()}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedSortFields = metadata.SortableFields
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedGroupByFields = metadata.GroupByFields
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allowedQuickFilters = metadata.QuickFilters
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Validate and strip non-whitelisted clauses; the list may carry duplicate keys, so
        // collapse them (first wins) before building the dictionary the domain expects.
        Dictionary<string, string>? validatedFilter = null;
        if (dto.Filter is { Count: > 0 })
        {
            validatedFilter = dto.Filter
                .Where(c => !string.IsNullOrEmpty(c.Key) && allowedFilterKeys.Contains(c.Key))
                .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.First().Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

            if (validatedFilter.Count == 0)
            {
                validatedFilter = null;
            }
        }

        // Validate sort fields
        string? validatedSort = null;
        if (!string.IsNullOrWhiteSpace(dto.Sort))
        {
            string[] sortParts = dto.Sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string[] validParts = sortParts
                .Where(p =>
                {
                    string fieldName = p.StartsWith('-') ? p[1..] : p;
                    return allowedSortFields.Contains(fieldName);
                })
                .ToArray();
            validatedSort = validParts.Length > 0 ? string.Join(',', validParts) : null;
        }

        // Validate group-by
        string? validatedGroupBy = !string.IsNullOrWhiteSpace(dto.GroupBy) && allowedGroupByFields.Contains(dto.GroupBy)
            ? dto.GroupBy
            : null;

        // Validate quick filters
        List<string>? validatedQuickFilters = dto.QuickFilters is { Count: > 0 }
            ? dto.QuickFilters.Where(allowedQuickFilters.Contains).ToList()
            : null;

        if (validatedQuickFilters is { Count: 0 })
        {
            validatedQuickFilters = null;
        }

        return new QueryRequest
        {
            Page = dto.Page,
            PageSize = dto.PageSize,
            Sort = validatedSort,
            Filter = validatedFilter,
            QuickFilters = validatedQuickFilters?.AsReadOnly(),
            GroupBy = validatedGroupBy,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation returned a non-success status {Status} (input length: {InputLength})")]
    private static partial void LogInvalidResponse(ILogger logger, int inputLength, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation timed out (input length: {InputLength})")]
    private static partial void LogTimeout(ILogger logger, int inputLength);
}
