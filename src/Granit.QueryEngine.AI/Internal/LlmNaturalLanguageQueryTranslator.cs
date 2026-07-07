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
        ArgumentNullException.ThrowIfNull(metadata);

        if (string.IsNullOrWhiteSpace(naturalLanguage))
        {
            return null;
        }

        using Activity? activity = QueryEngineAIActivitySource.Source.StartActivity(QueryEngineAIActivitySource.Translate);
        long startTimestamp = Stopwatch.GetTimestamp();
        string? tenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() : null;
        activity?.SetTag("tenant_id", tenantId ?? "global");
        activity?.SetTag("input_length", naturalLanguage.Length);

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
                activity?.SetStatus(ActivityStatusCode.Error, result.Status.ToString());
                return null;
            }

            metrics?.RecordTranslationExecuted(tenantId);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return ValidateAndConvert(result.Value!, metadata);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogTimeout(logger, naturalLanguage.Length);
            metrics?.RecordTranslationFailed(tenantId, "timeout");
            activity?.SetStatus(ActivityStatusCode.Error, "timeout");
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

    // Used by the fuzz harness (Granit.QueryEngine.AI.Fuzz) to exercise the
    // JSON-parse + whitelist-validation path without going through the LLM.
    internal static bool TryDeserializeAndConvert(string json, QueryMetadata metadata, out QueryRequest? result)
    {
        LlmQueryPayload? dto = System.Text.Json.JsonSerializer.Deserialize<LlmQueryPayload>(json);
        if (dto is null)
        {
            result = null;
            return false;
        }

        result = ValidateAndConvert(dto, metadata);
        return true;
    }

    private static QueryRequest ValidateAndConvert(LlmQueryPayload dto, QueryMetadata metadata)
    {
        // Single shared whitelist gate (CWE-20, LLM02): field/operator whitelisting, sort/
        // group-by/quick-filter stripping and pagination clamping live in
        // QueryRequestSanitizer so this path cannot drift from the query_data tool path.
        QueryRequestCandidate candidate = new()
        {
            Page = dto.Page,
            PageSize = dto.PageSize,
            Sort = dto.Sort,
            Filter = dto.Filter?.ConvertAll(c => new KeyValuePair<string, string>(c.Key, c.Value)),
            QuickFilters = dto.QuickFilters,
            GroupBy = dto.GroupBy,
        };

        return QueryRequestSanitizer.Sanitize(candidate, metadata).Request;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation returned a non-success status {Status} (input length: {InputLength})")]
    private static partial void LogInvalidResponse(ILogger logger, int inputLength, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation timed out (input length: {InputLength})")]
    private static partial void LogTimeout(ILogger logger, int inputLength);
}
