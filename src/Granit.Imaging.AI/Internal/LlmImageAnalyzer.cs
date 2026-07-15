using System.Diagnostics;
using System.Text.RegularExpressions;
using Granit.AI;
using Granit.Imaging.AI.Diagnostics;
using Granit.Imaging.AI.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Imaging.AI.Internal;

/// <summary>
/// Multimodal implementation of <see cref="IAIImageAnalyzer"/> riding the ADR-064
/// structured-output primitive: schema pinning, fence-strip fallback, PII-safe failure
/// mapping, quota, and usage stamping all come from <see cref="IStructuredCompletion"/>.
/// The model output is still treated as untrusted and sanitized before it reaches callers.
/// </summary>
internal sealed partial class LlmImageAnalyzer(
    IStructuredCompletion structuredCompletion,
    IOptions<ImagingAIOptions> options,
    ImagingAIMetrics metrics,
    ICurrentTenant currentTenant,
    ILogger<LlmImageAnalyzer> logger) : IAIImageAnalyzer
{
    // Task instruction only — the output shape is pinned by the primitive's JSON schema
    // (provider-enforced when the model supports structured output, in-prompt otherwise).
    private const string AnalysisInstruction =
        "Analyze the attached image. Produce a detailed natural-language description of its "
        + "content, the list of objects it contains, semantic tags for classification and "
        + "search, and a concise suggested alt text for accessibility (WCAG 2.1).";

    private static readonly HashSet<string> AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "image/avif", "image/gif", "image/bmp", "image/tiff"];

    /// <inheritdoc />
    public async Task<ImageAnalysis> AnalyzeAsync(
        ReadOnlyMemory<byte> imageData,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        ImagingAIOptions opts = options.Value;

        // First statement, before any DataContent is built: the image arrives already
        // materialized (the byte source owns the original allocation guard), but failing
        // here avoids the ~1.33x base64 expansion, the provider serialization, and a
        // wasted round-trip.
        if (opts.MaxImageBytes > 0 && imageData.Length > opts.MaxImageBytes)
        {
            throw new ArgumentException(
                $"Image size ({imageData.Length} bytes) exceeds the configured MaxImageBytes ({opts.MaxImageBytes}).",
                nameof(imageData));
        }

        string normalizedContentType = NormalizeContentType(contentType);
        string? tenantId = currentTenant.IsAvailable ? currentTenant.Id?.ToString() : null;
        long startTimestamp = Stopwatch.GetTimestamp();

        using Activity? activity = ImagingAIActivitySource.Source
            .StartActivity(ImagingAIActivitySource.AnalyzeOperation);
        activity?.SetTag(ImagingAIActivitySource.TagContentType, normalizedContentType);
        activity?.SetTag(ImagingAIActivitySource.TagImageSizeBytes, imageData.Length);

        LogAnalysisStarted(normalizedContentType, imageData.Length);

        try
        {
            // Two timeouts stack here: this one (Imaging:AI TimeoutSeconds) and the
            // primitive's StructuredCompletionOptions.TimeoutSeconds — the lower wins.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            StructuredCompletionResult<LlmAnalysisResponse> completion = await structuredCompletion
                .CompleteAsync<LlmAnalysisResponse>(
                    new StructuredCompletionRequest
                    {
                        Instruction = AnalysisInstruction,
                        Attachments = [new DataContent(imageData, contentType)],
                        WorkspaceName = opts.WorkspaceName,
                    },
                    timeoutCts.Token)
                .ConfigureAwait(false);

            ImageAnalysis result = MapResult(completion);

            metrics.RecordAnalysisCompleted(tenantId, normalizedContentType);
            metrics.RecordAnalysisDuration(tenantId, normalizedContentType, Stopwatch.GetElapsedTime(startTimestamp));

            LogAnalysisCompleted(result.DetectedObjects.Count, result.Tags.Count);

            return result;
        }
        catch
        {
            metrics.RecordAnalysisFailure(tenantId, normalizedContentType);
            throw;
        }
    }

    private static string NormalizeContentType(string contentType) =>
        AllowedContentTypes.Contains(contentType) ? contentType : "other";

    // Preserves the analyzer's exception-based contract over the primitive's status codes;
    // ErrorMessage is PII-safe by the primitive's contract (never echoes model output).
    private static ImageAnalysis MapResult(StructuredCompletionResult<LlmAnalysisResponse> completion) =>
        completion.Status switch
        {
            StructuredCompletionStatus.Succeeded => Sanitize(completion.Value!),
            StructuredCompletionStatus.ModelRefused =>
                throw new InvalidOperationException("The AI model returned an empty response."),
            StructuredCompletionStatus.SchemaViolation =>
                throw new InvalidOperationException("Failed to parse the AI model response as JSON."),
            _ => throw new InvalidOperationException(
                completion.ErrorMessage ?? "The AI request failed due to a transport or provider error."),
        };

    private static ImageAnalysis Sanitize(LlmAnalysisResponse parsed) =>
        new(
            SanitizeText(parsed.Description),
            SanitizeList(parsed.DetectedObjects),
            SanitizeList(parsed.Tags),
            parsed.SuggestedAltText is not null ? SanitizeText(parsed.SuggestedAltText, 500) : null);

    private static string SanitizeText(string? value, int maxLength = 2000)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string cleaned = ControlCharPattern().Replace(value, string.Empty);
        return cleaned.Length > maxLength ? cleaned[..maxLength] : cleaned;
    }

    private static List<string> SanitizeList(
        IReadOnlyList<string>? items,
        int maxItems = 50,
        int maxItemLength = 200)
    {
        if (items is null or { Count: 0 })
        {
            return [];
        }

        return items
            .Take(maxItems)
            .Select(item => SanitizeText(item, maxItemLength))
            .Where(item => item.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]")]
    private static partial Regex ControlCharPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting AI image analysis (contentType={ContentType}, size={SizeBytes} bytes)")]
    private partial void LogAnalysisStarted(string contentType, int sizeBytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "AI image analysis completed (objects={ObjectCount}, tags={TagCount})")]
    private partial void LogAnalysisCompleted(int objectCount, int tagCount);
}
