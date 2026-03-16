using System.Text.Json;
using Granit.AI;
using Granit.BlobStorage.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AI.Internal;

/// <summary>
/// LLM-based blob classifier that also participates in the <see cref="IBlobValidator"/> pipeline.
/// </summary>
/// <remarks>
/// <para>
/// As <see cref="IBlobValidator"/> (Order = 100): runs after cheaper validators (magic bytes, max size).
/// Classification never rejects a file — it only tags it. If PII is detected in the filename
/// and <see cref="BlobStorageAIOptions.EnablePiiDetection"/> is enabled, the validation step
/// returns a failure to prevent storage of files with PII-leaking names.
/// </para>
/// <para>
/// As <see cref="IAIBlobClassifier"/>: provides classification on demand outside the pipeline.
/// </para>
/// </remarks>
internal sealed partial class AIBlobClassifierService(
    IAIChatClientFactory chatClientFactory,
    IOptions<BlobStorageAIOptions> options,
    ILogger<AIBlobClassifierService> logger) : IAIBlobClassifier, IBlobValidator
{
    private static readonly BlobClassification UnknownClassification = new(
        Category: "unknown",
        Confidence: 0.0,
        DetectedTags: [],
        ContainsPiiInFileName: false);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async Task<BlobClassification> ClassifyAsync(
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(contentType);

        BlobStorageAIOptions config = options.Value;

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, cancellationToken)
                .ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            string prompt = BuildClassificationPrompt(fileName, contentType);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            return ParseClassificationResponse(responseText);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogClassificationTimeout(logger, fileName, config.TimeoutSeconds);
            return UnknownClassification;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogClassificationFailure(logger, fileName, ex);
            return UnknownClassification;
        }
    }

    /// <inheritdoc/>
    public async Task<BlobValidationResult> ValidateAsync(
        BlobValidationContext context,
        CancellationToken cancellationToken = default)
    {
        BlobClassification classification = await ClassifyAsync(
            context.Descriptor.OriginalFileName,
            context.Descriptor.DeclaredContentType,
            cancellationToken).ConfigureAwait(false);

        BlobStorageAIOptions config = options.Value;

        if (config.EnablePiiDetection && classification.ContainsPiiInFileName)
        {
            LogPiiDetected(logger, context.Descriptor.OriginalFileName);
            return BlobValidationResult.Failure(
                $"Filename '{context.Descriptor.OriginalFileName}' appears to contain personally identifiable information. " +
                "Rename the file before uploading to comply with data protection requirements.");
        }

        return BlobValidationResult.Success();
    }

    internal static string BuildClassificationPrompt(string fileName, string contentType) =>
        $$"""
        Given filename '{{fileName}}' and content type '{{contentType}}', classify this file.
        Return JSON only, no markdown fences: {"category": "<string>", "confidence": <0.0-1.0>, "tags": ["<string>"], "containsPiiInFileName": <true|false>}
        Categories: invoice, identity_document, photo, contract, report, spreadsheet, presentation, archive, code, other.
        For PII detection, check if the filename contains patterns resembling: social security numbers, email addresses, phone numbers, national ID numbers, or full personal names.
        """;

    internal static BlobClassification ParseClassificationResponse(string responseText)
    {
        string trimmed = responseText.Trim();

        // Strip markdown code fences if the LLM wraps the JSON anyway
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = trimmed.IndexOf('\n');
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline >= 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }

        try
        {
            ClassificationJson? parsed = JsonSerializer.Deserialize<ClassificationJson>(trimmed, JsonOptions);

            if (parsed is null)
            {
                return UnknownClassification;
            }

            return new BlobClassification(
                Category: parsed.Category ?? "unknown",
                Confidence: Math.Clamp(parsed.Confidence, 0.0, 1.0),
                DetectedTags: parsed.Tags ?? [],
                ContainsPiiInFileName: parsed.ContainsPiiInFileName);
        }
        catch (JsonException)
        {
            return UnknownClassification;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI blob classification timed out after {TimeoutSeconds}s for file '{FileName}'")]
    private static partial void LogClassificationTimeout(ILogger logger, string fileName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI blob classification failed for file '{FileName}'")]
    private static partial void LogClassificationFailure(ILogger logger, string fileName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PII detected in filename '{FileName}' — upload rejected")]
    private static partial void LogPiiDetected(ILogger logger, string fileName);

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    private sealed class ClassificationJson
    {
        public string? Category { get; set; }
        public double Confidence { get; set; }
        public List<string>? Tags { get; set; }
        public bool ContainsPiiInFileName { get; set; }
    }
}
