using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.MultiTenancy;
using Granit.QueryEngine.AI.Diagnostics;
using Granit.QueryEngine.AI.Options;
using Granit.QueryEngine.Meta;
using Granit.Timing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.AI.Internal;

/// <summary>
/// LLM-backed implementation of <see cref="INaturalLanguageQueryTranslator"/>.
/// Sends query metadata to the LLM as schema context and parses the structured JSON response.
/// </summary>
internal sealed partial class LlmNaturalLanguageQueryTranslator(
    IAIChatClientFactory chatClientFactory,
    IOptions<QueryEngineAIOptions> options,
    ILogger<LlmNaturalLanguageQueryTranslator> logger,
    IClock clock,
    QueryEngineAIMetrics? metrics = null,
    ICurrentTenant? currentTenant = null) : INaturalLanguageQueryTranslator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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

        try
        {
            QueryEngineAIOptions opts = options.Value;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            IChatClient chatClient = await chatClientFactory
                .CreateAsync(opts.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string systemPrompt = BuildSystemPrompt(metadata);

            // Sanitize user input via PromptBuilder to mitigate prompt injection
            var userPb = new PromptBuilder(maxInputLength: 2_000);
            userPb.AppendUserTextBlock("Query", naturalLanguage);

            List<ChatMessage> messages =
            [
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPb.Build()),
            ];

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string rawJson = response.Text ?? string.Empty;
            string json = StripMarkdownFences(rawJson);

            QueryRequest? request = TryDeserializeAndConvert(json, metadata, out bool deserializedToNull);
            if (deserializedToNull)
            {
                LogInvalidResponse(logger, naturalLanguage.Length);
                metrics?.RecordTranslationFailed(tenantId, "invalid_response");
                return null;
            }

            metrics?.RecordTranslationExecuted(tenantId, "success");
            return request;
        }
        catch (OperationCanceledException)
        {
            LogTimeout(logger, naturalLanguage.Length);
            metrics?.RecordTranslationFailed(tenantId, "timeout");
            return null;
        }
        catch (JsonException ex)
        {
            LogJsonParseError(logger, naturalLanguage.Length, ex);
            metrics?.RecordTranslationFailed(tenantId, "json_parse_error");
            return null;
        }
        catch (Exception ex)
        {
            LogTranslationError(logger, naturalLanguage.Length, ex);
            metrics?.RecordTranslationFailed(tenantId, "error");
            return null;
        }
        finally
        {
            double elapsed = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            metrics?.RecordTranslationDuration(tenantId, elapsed);
        }
    }

    internal string BuildSystemPrompt(QueryMetadata metadata)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a query translator. Convert the user's natural language phrase into a JSON object matching this schema:");
        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"page\": <int or null>,");
        sb.AppendLine("  \"pageSize\": <int or null>,");
        sb.AppendLine("  \"sort\": \"<comma-separated fields, prefix - for desc>\" or null,");
        sb.AppendLine("  \"filter\": { \"<field.operator>\": \"<value>\", ... } or null,");
        sb.AppendLine("  \"quickFilters\": [\"<name>\", ...] or null,");
        sb.AppendLine("  \"groupBy\": \"<field>\" or null");
        sb.AppendLine("}");
        sb.AppendLine("```");
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
        sb.AppendLine($"Current date: {clock.Now:yyyy-MM-dd}. Use this for relative date references (\"this week\", \"last month\").");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Return ONLY valid JSON, no explanation.");
        sb.AppendLine("- Use only the fields and operators listed above.");
        sb.AppendLine("- Omit properties that are not needed (use null).");
        sb.AppendLine("- For date ranges, use gte/lte operators with ISO 8601 dates.");

        return sb.ToString();
    }

    internal static string StripMarkdownFences(string text) =>
        LlmResponseHelper.StripMarkdownCodeFences(text);

    /// <summary>
    /// Deserializes an LLM JSON payload into a validated <see cref="QueryRequest"/>.
    /// Returns <c>null</c> when deserialization yields a <c>null</c> payload (signalled via
    /// <paramref name="deserializedToNull"/> = <c>true</c>) so callers can distinguish that case
    /// from a valid empty result. Throws <see cref="JsonException"/> on malformed JSON — the
    /// production caller catches it; the fuzz harness intentionally swallows it.
    /// </summary>
    internal static QueryRequest? TryDeserializeAndConvert(string json, QueryMetadata metadata, out bool deserializedToNull)
    {
        LlmQueryPayload? dto = JsonSerializer.Deserialize<LlmQueryPayload>(json, JsonOptions);
        if (dto is null)
        {
            deserializedToNull = true;
            return null;
        }

        deserializedToNull = false;
        return ValidateAndConvert(dto, metadata);
    }

    private static QueryRequest? ValidateAndConvert(LlmQueryPayload dto, QueryMetadata metadata)
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

        // Validate and strip non-whitelisted fields from LLM output
        Dictionary<string, string>? validatedFilter = dto.Filter is { Count: > 0 }
            ? dto.Filter
                .Where(kv => allowedFilterKeys.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value)
            : null;

        if (validatedFilter is { Count: 0 })
        {
            validatedFilter = null;
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
            ? dto.QuickFilters.Where(qf => allowedQuickFilters.Contains(qf)).ToList()
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation returned invalid response (input length: {InputLength})")]
    private static partial void LogInvalidResponse(ILogger logger, int inputLength);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation timed out (input length: {InputLength})")]
    private static partial void LogTimeout(ILogger logger, int inputLength);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation failed to parse JSON (input length: {InputLength})")]
    private static partial void LogJsonParseError(ILogger logger, int inputLength, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "NLQ translation failed (input length: {InputLength})")]
    private static partial void LogTranslationError(ILogger logger, int inputLength, Exception exception);
}
