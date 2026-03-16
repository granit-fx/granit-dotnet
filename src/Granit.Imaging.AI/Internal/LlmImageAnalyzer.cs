using System.Text.Json;
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

    /// <inheritdoc />
    public async Task<ImageAnalysis> AnalyzeAsync(
        ReadOnlyMemory<byte> imageData,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentType);

        ImagingAIOptions opts = options.Value;

        using System.Diagnostics.Activity? activity = ImagingAIActivitySource.Source.StartActivity("ImageAnalysis.Analyze");
        activity?.SetTag("imaging.ai.content_type", contentType);
        activity?.SetTag("imaging.ai.image_size_bytes", imageData.Length);

        LogAnalysisStarted(contentType, imageData.Length);

        IChatClient client = await chatClientFactory
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

        LogAnalysisCompleted(result.DetectedObjects.Count, result.Tags.Count);

        return result;
    }

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
            parsed.Description ?? string.Empty,
            parsed.DetectedObjects ?? [],
            parsed.Tags ?? [],
            parsed.SuggestedAltText);
    }

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
