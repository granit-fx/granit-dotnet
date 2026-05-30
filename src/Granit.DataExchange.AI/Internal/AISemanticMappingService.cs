using System.Globalization;
using System.Text;
using Granit.AI;
using Granit.DataExchange.AI.Options;
using Granit.DataExchange.AI.Schema;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.AI.Internal;

/// <summary>
/// AI-powered implementation of <see cref="ISemanticMappingService"/> that suggests
/// column-to-property mappings via the <see cref="IStructuredCompletion"/> primitive (ADR-064).
/// </summary>
/// <remarks>
/// <para>
/// Only column headers and (opt-in) preview rows travel as untrusted
/// <see cref="StructuredCompletionRequest.Content"/>; the developer-controlled target schema and
/// confidence threshold are the <see cref="StructuredCompletionRequest.Instruction"/>. By default
/// no business data is sent (headers-only mode, GDPR-safe).
/// </para>
/// <para>
/// On any non-success outcome the service gracefully degrades to an empty result set. Returned
/// mappings are intersected with the known source columns and target properties, so a model that
/// invents or echoes an out-of-schema name contributes nothing.
/// </para>
/// </remarks>
internal sealed partial class AISemanticMappingService(
    IStructuredCompletion structuredCompletion,
    IOptions<DataExchangeAIOptions> options,
    ILogger<AISemanticMappingService> logger) : ISemanticMappingService
{
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

        // Only include preview rows if the option is explicitly enabled (GDPR opt-in),
        // truncated to the configured limit to bound prompt size.
        IReadOnlyList<string[]>? effectivePreview = GetEffectivePreview(opts, previewRows);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = BuildInstruction(targetFields, opts.MinConfidenceScore),
            Content = BuildContent(headers, effectivePreview),
            ContentLabel = "Import sample",
            WorkspaceName = opts.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<MappingSuggestionsResponse> result = await structuredCompletion
                .CompleteAsync<MappingSuggestionsResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.Status != StructuredCompletionStatus.Succeeded)
            {
                LogSemanticMappingRejected(result.Status.ToString());
                return [];
            }

            return Filter(result.Value!, headers, targetFields, opts.MinConfidenceScore);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogSemanticMappingTimeout(opts.TimeoutSeconds);
            return [];
        }
    }

    /// <summary>
    /// Projects the schema-pinned response onto the public surface: clamps scores, drops
    /// below-threshold and out-of-schema entries, and orders by descending confidence.
    /// </summary>
    private List<SemanticMappingSuggestion> Filter(
        MappingSuggestionsResponse response,
        IReadOnlyList<string> headers,
        IReadOnlyList<ImportFieldMetadata> targetFields,
        double minConfidenceScore)
    {
        HashSet<string> validTargets = new(targetFields.Select(f => f.PropertyPath), StringComparer.OrdinalIgnoreCase);
        HashSet<string> validSources = new(headers, StringComparer.OrdinalIgnoreCase);

        var filtered = response.Mappings
            .Where(m => !string.IsNullOrEmpty(m.Source) && !string.IsNullOrEmpty(m.Target))
            .Select(m => new SemanticMappingSuggestion(m.Source, m.Target, Math.Clamp(m.Score, 0.0, 1.0)))
            .Where(s => s.Score >= minConfidenceScore
                        && validTargets.Contains(s.TargetProperty)
                        && validSources.Contains(s.SourceColumn))
            .OrderByDescending(s => s.Score)
            .ToList();

        LogSuggestionsFiltered(response.Mappings.Count, filtered.Count, minConfidenceScore);

        return filtered;
    }

    private static string BuildInstruction(IReadOnlyList<ImportFieldMetadata> targetFields, double minConfidenceScore)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a data-mapping assistant. Match the source columns in the data block to the");
        sb.AppendLine("target entity properties below, assigning each match a confidence score from 0.0 to 1.0.");
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
            sb.Append(SanitizeCellValue(field.DisplayName ?? "-"));
            sb.Append(" | ");
            sb.Append(SanitizeCellValue(field.Description ?? "-"));
            sb.Append(" | ");
            sb.Append(field.IsRequired ? "Yes" : "No");
            sb.AppendLine(" |");
        }

        sb.AppendLine();
        sb.Append(CultureInfo.InvariantCulture, $"Only include confident matches (score >= {minConfidenceScore:F1}); ");
        sb.AppendLine("use the exact source-column and target-property names. Return no mapping for columns you cannot place.");

        return sb.ToString();
    }

    private static string BuildContent(IReadOnlyList<string> headers, IReadOnlyList<string[]>? previewRows)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Source columns:");
        foreach (string header in headers)
        {
            sb.Append("- ");
            sb.AppendLine(SanitizeCellValue(header));
        }

        if (previewRows is { Count: > 0 })
        {
            AppendPreviewTable(sb, headers, previewRows);
        }

        return sb.ToString();
    }

    private static void AppendPreviewTable(StringBuilder sb, IReadOnlyList<string> headers, IReadOnlyList<string[]> previewRows)
    {
        sb.AppendLine();
        sb.AppendLine("Sample data (first rows):");
        sb.AppendLine("| " + string.Join(" | ", headers.Select(SanitizeCellValue)) + " |");
        sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

        foreach (string[] row in previewRows)
        {
            sb.Append("| ");
            for (int i = 0; i < headers.Count; i++)
            {
                string cellValue = i < row.Length ? row[i] : "-";
                sb.Append(SanitizeCellValue(cellValue));
                sb.Append(i < headers.Count - 1 ? " | " : " |");
            }

            sb.AppendLine();
        }
    }

    private static string SanitizeCellValue(string value) =>
        value.Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal)
             .Replace("|", "\\|", StringComparison.Ordinal)
             .Replace("\n", " ", StringComparison.Ordinal)
             .Replace("\r", " ", StringComparison.Ordinal);

    private static IReadOnlyList<string[]>? GetEffectivePreview(
        DataExchangeAIOptions opts, IReadOnlyList<string[]>? previewRows)
    {
        if (!opts.IncludePreviewRows || previewRows is null)
        {
            return null;
        }

        return previewRows.Count <= opts.PreviewRowCount
            ? previewRows
            : previewRows.Take(opts.PreviewRowCount).ToList();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Semantic mapping: {TotalCount} suggestions from LLM, {FilteredCount} after filtering (min score: {MinScore:F2})")]
    private partial void LogSuggestionsFiltered(int totalCount, int filteredCount, double minScore);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI semantic mapping rejected (status: {Status}), returning empty suggestions (graceful degradation)")]
    private partial void LogSemanticMappingRejected(string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI semantic mapping timed out after {TimeoutSeconds}s, returning empty suggestions (graceful degradation)")]
    private partial void LogSemanticMappingTimeout(int timeoutSeconds);
}
