using System.Text;
using System.Text.Json;
using Granit.AI;
using Granit.Querying.AI.Options;
using Granit.Querying.Meta;
using Granit.Timing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Querying.AI.Internal;

/// <summary>
/// LLM-backed implementation of <see cref="INaturalLanguageQueryTranslator"/>.
/// Sends query metadata to the LLM as schema context and parses the structured JSON response.
/// </summary>
internal sealed partial class LlmNaturalLanguageQueryTranslator(
    IAIChatClientFactory chatClientFactory,
    IOptions<QueryingAIOptions> options,
    ILogger<LlmNaturalLanguageQueryTranslator> logger,
    IClock clock) : INaturalLanguageQueryTranslator
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

        try
        {
            QueryingAIOptions opts = options.Value;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            IChatClient chatClient = await chatClientFactory
                .CreateAsync(opts.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string systemPrompt = BuildSystemPrompt(metadata);

            List<ChatMessage> messages =
            [
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, naturalLanguage),
            ];

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string rawJson = response.Text ?? string.Empty;
            string json = StripMarkdownFences(rawJson);

            QueryRequestDto? dto = JsonSerializer.Deserialize<QueryRequestDto>(json, JsonOptions);
            if (dto is null)
            {
                LogInvalidResponse(logger, naturalLanguage);
                return null;
            }

            return ToQueryRequest(dto);
        }
        catch (OperationCanceledException)
        {
            LogTimeout(logger, naturalLanguage);
            return null;
        }
        catch (JsonException ex)
        {
            LogJsonParseError(logger, naturalLanguage, ex);
            return null;
        }
        catch (Exception ex)
        {
            LogTranslationError(logger, naturalLanguage, ex);
            return null;
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

    internal static string StripMarkdownFences(string text)
    {
        string trimmed = text.Trim();

        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            // Remove opening fence (```json or ```)
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            // Remove closing fence
            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3].TrimEnd();
            }
        }

        return trimmed;
    }

    private static QueryRequest ToQueryRequest(QueryRequestDto dto) =>
        new()
        {
            Page = dto.Page,
            PageSize = dto.PageSize,
            Sort = dto.Sort,
            Filter = dto.Filter is { Count: > 0 }
                ? new Dictionary<string, string>(dto.Filter)
                : null,
            QuickFilters = dto.QuickFilters is { Count: > 0 }
                ? dto.QuickFilters.AsReadOnly()
                : null,
            GroupBy = dto.GroupBy,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation returned invalid response for input: {NaturalLanguage}")]
    private static partial void LogInvalidResponse(ILogger logger, string naturalLanguage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation timed out for input: {NaturalLanguage}")]
    private static partial void LogTimeout(ILogger logger, string naturalLanguage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "NLQ translation failed to parse JSON for input: {NaturalLanguage}")]
    private static partial void LogJsonParseError(ILogger logger, string naturalLanguage, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "NLQ translation failed for input: {NaturalLanguage}")]
    private static partial void LogTranslationError(ILogger logger, string naturalLanguage, Exception exception);
}
