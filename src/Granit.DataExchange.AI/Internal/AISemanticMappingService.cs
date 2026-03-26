using System.Text;
using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.DataExchange.AI.Options;
using Granit.DataExchange.Import.Mapping;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.AI.Internal;

/// <summary>
/// AI-powered implementation of <see cref="ISemanticMappingService"/> that uses
/// an LLM via <see cref="IAIChatClientFactory"/> to suggest column-to-property mappings.
/// </summary>
/// <remarks>
/// Only column headers and field metadata are sent to the LLM — never business data (GDPR safe).
/// On failure, gracefully degrades to an empty result set.
/// </remarks>
internal sealed partial class AISemanticMappingService(
    IAIChatClientFactory chatClientFactory,
    IOptions<DataExchangeAIOptions> options,
    ILogger<AISemanticMappingService> logger) : ISemanticMappingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public Task<IReadOnlyList<SemanticMappingSuggestion>> SuggestSemanticMappingsAsync(
        IReadOnlyList<string> headers,
        IReadOnlyList<ImportFieldMetadata> targetFields,
        CancellationToken cancellationToken = default)
        => SuggestSemanticMappingsAsync(headers, targetFields, previewRows: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SemanticMappingSuggestion>> SuggestSemanticMappingsAsync(
        IReadOnlyList<string> headers,
        IReadOnlyList<ImportFieldMetadata> targetFields,
        IReadOnlyList<string[]>? previewRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(targetFields);

        if (headers.Count == 0 || targetFields.Count == 0)
        {
            return [];
        }

        DataExchangeAIOptions opts = options.Value;

        // Only include preview rows if the option is explicitly enabled (GDPR opt-in)
        IReadOnlyList<string[]>? effectivePreview = opts.IncludePreviewRows ? previewRows : null;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(opts.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(headers, targetFields, opts.MinConfidenceScore, effectivePreview);

            ChatResponse response = await chatClient
                .GetResponseAsync(prompt, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            LogLlmResponseReceived(responseText.Length);

            IReadOnlyList<SemanticMappingSuggestion> suggestions = ParseSuggestions(responseText);

            var filtered = suggestions
                .Where(s => s.Score >= opts.MinConfidenceScore)
                .OrderByDescending(s => s.Score)
                .ToList();

            LogSuggestionsFiltered(suggestions.Count, filtered.Count, opts.MinConfidenceScore);

            return filtered;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogSemanticMappingFailed(ex);
            return [];
        }
    }

    internal static string BuildPrompt(
        IReadOnlyList<string> headers,
        IReadOnlyList<ImportFieldMetadata> targetFields,
        double minConfidenceScore,
        IReadOnlyList<string[]>? previewRows = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a data mapping assistant. Match source CSV/Excel columns to target entity properties.");
        sb.AppendLine();

        // Wrap user-controlled headers in a PromptBuilder data block
        var headerPb = new PromptBuilder(maxInputLength: 10_000);
        headerPb.AppendUserDataMap("Source columns", headers.Select(h => new KeyValuePair<string, string?>(h, null)));
        sb.Append(headerPb.Build());

        // Include preview rows when provided (opt-in, caller is responsible for GDPR compliance)
        if (previewRows is { Count: > 0 })
        {
            AppendPreviewTable(sb, headers, previewRows);
        }

        sb.AppendLine();
        sb.AppendLine("Target properties:");
        sb.AppendLine("| Property | Type | Display Name | Description | Required |");
        sb.AppendLine("|----------|------|-------------|-------------|----------|");

        foreach (ImportFieldMetadata field in targetFields)
        {
            sb.Append("| ");
            sb.Append(field.PropertyPath);
            sb.Append(" | ");
            sb.Append(field.ClrTypeName);
            sb.Append(" | ");
            sb.Append(field.DisplayName ?? "-");
            sb.Append(" | ");
            sb.Append(field.Description ?? "-");
            sb.Append(" | ");
            sb.Append(field.IsRequired ? "Yes" : "No");
            sb.AppendLine(" |");
        }

        sb.AppendLine();
        sb.AppendLine("Return a JSON array of mappings. Each mapping has:");
        sb.AppendLine("""- "source": exact source column name""");
        sb.AppendLine("""- "target": exact target property path""");
        sb.AppendLine("""- "score": confidence score 0.0 to 1.0""");
        sb.AppendLine();
        sb.Append($"Only include confident matches (score >= {minConfidenceScore:F1}). ");
        sb.AppendLine("Return [] if no good matches found.");
        sb.AppendLine("Return ONLY the JSON array, no markdown fences or extra text.");

        return sb.ToString();
    }

    private static void AppendPreviewTable(StringBuilder sb, IReadOnlyList<string> headers, IReadOnlyList<string[]> previewRows)
    {
        sb.AppendLine();
        sb.AppendLine("Sample data (first rows):");
        sb.AppendLine("| " + string.Join(" | ", headers) + " |");
        sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

        foreach (string[] row in previewRows)
        {
            sb.Append("| ");
            for (int i = 0; i < headers.Count; i++)
            {
                sb.Append(i < row.Length ? row[i] : "-");
                sb.Append(i < headers.Count - 1 ? " | " : " |");
            }

            sb.AppendLine();
        }
    }

    internal static IReadOnlyList<SemanticMappingSuggestion> ParseSuggestions(string responseText)
    {
        string trimmed = LlmResponseHelper.StripMarkdownCodeFences(responseText);

        // Find the JSON array boundaries
        int startIndex = trimmed.IndexOf('[');
        int endIndex = trimmed.LastIndexOf(']');

        if (startIndex < 0 || endIndex < 0 || endIndex <= startIndex)
        {
            return [];
        }

        string jsonArray = trimmed[startIndex..(endIndex + 1)];

        List<MappingDto>? dtos = JsonSerializer.Deserialize<List<MappingDto>>(jsonArray, JsonOptions);

        if (dtos is null)
        {
            return [];
        }

        return dtos
            .Where(d => d.Source is not null && d.Target is not null)
            .Select(d => new SemanticMappingSuggestion(d.Source!, d.Target!, d.Score))
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "LLM response received for semantic mapping ({ResponseLength} chars)")]
    private partial void LogLlmResponseReceived(int responseLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Semantic mapping: {TotalCount} suggestions from LLM, {FilteredCount} after filtering (min score: {MinScore:F2})")]
    private partial void LogSuggestionsFiltered(int totalCount, int filteredCount, double minScore);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI semantic mapping failed, returning empty suggestions (graceful degradation)")]
    private partial void LogSemanticMappingFailed(Exception exception);

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    internal sealed class MappingDto
    {
        public string? Source { get; set; }
        public string? Target { get; set; }
        public double Score { get; set; }
    }
}
