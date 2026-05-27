using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Granit.AI;
using Granit.Imaging.AI.Diagnostics;
using Granit.Imaging.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Imaging.AI.Internal;

/// <summary>
/// Multimodal LLM-based implementation of <see cref="IAIImageAnalyzer"/>.
/// Sends image bytes as a <see cref="DataContent"/> message to a vision-capable model.
/// </summary>
internal sealed partial class LlmImageAnalyzer(
    IAIChatClientFactory chatClientFactory,
    IOptions<ImagingAIOptions> options,
    ImagingAIMetrics metrics,
    ILogger<LlmImageAnalyzer> logger) : IAIImageAnalyzer
{
    private const string AnalysisPrompt = """
        Analyze this image and return a JSON object with the following structure:
        {
          "description": "A detailed natural-language description of the image content",
          "detectedObjects": ["list", "of", "objects", "in", "the", "image"],
          "tags": ["semantic", "tags", "for", "classification"],
          "suggestedAltText": "Concise alt text for accessibility (WCAG 2.1)"
        }
        Return ONLY the JSON object, no markdown fences, no explanation.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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
        string normalizedContentType = NormalizeContentType(contentType);
        long startTimestamp = Stopwatch.GetTimestamp();

        using Activity? activity = ImagingAIActivitySource.Source.StartActivity("ImageAnalysis.Analyze");
        activity?.SetTag("imaging.ai.content_type", normalizedContentType);
        activity?.SetTag("imaging.ai.image_size_bytes", imageData.Length);

        LogAnalysisStarted(normalizedContentType, imageData.Length);

        try
        {
            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient client = await chatClientFactory
                .CreateAsync(opts.WorkspaceName, cancellationToken)
                .ConfigureAwait(false);

            var message = new ChatMessage(ChatRole.User,
            [
                new TextContent(AnalysisPrompt),
                new DataContent(imageData, contentType),
            ]);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(opts.TimeoutSeconds));

            ChatResponse response = await client
                .GetResponseAsync([message], cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);

            string json = response.Text ?? throw new InvalidOperationException("The AI model returned an empty response.");

            ImageAnalysis result = Deserialize(json);

            metrics.RecordAnalysisCompleted(tenantId: null, normalizedContentType);
            metrics.RecordAnalysisDuration(tenantId: null, normalizedContentType, Stopwatch.GetElapsedTime(startTimestamp));

            LogAnalysisCompleted(result.DetectedObjects.Count, result.Tags.Count);

            return result;
        }
        catch
        {
            metrics.RecordAnalysisFailure(tenantId: null, normalizedContentType);
            throw;
        }
    }

    private static string NormalizeContentType(string contentType) =>
        AllowedContentTypes.Contains(contentType) ? contentType : "other";

    private static ImageAnalysis Deserialize(string json)
    {
        // Strip markdown fences if the model wraps the JSON despite instructions
        ReadOnlySpan<char> trimmed = json.AsSpan().Trim();
        if (trimmed.StartsWith("```"))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed[..^3].TrimEnd();
            }
        }

        LlmAnalysisResponse? parsed = JsonSerializer.Deserialize<LlmAnalysisResponse>(trimmed, JsonOptions)
            ?? throw new InvalidOperationException("Failed to parse the AI model response as JSON.");

        return new ImageAnalysis(
            SanitizeText(parsed.Description),
            SanitizeList(parsed.DetectedObjects),
            SanitizeList(parsed.Tags),
            parsed.SuggestedAltText is not null ? SanitizeText(parsed.SuggestedAltText, 500) : null);
    }

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

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    private sealed record LlmAnalysisResponse(
        string? Description,
        IReadOnlyList<string>? DetectedObjects,
        IReadOnlyList<string>? Tags,
        string? SuggestedAltText);
}
