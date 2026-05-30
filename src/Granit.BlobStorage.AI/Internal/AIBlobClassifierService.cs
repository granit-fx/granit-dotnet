using Granit.AI;
using Granit.BlobStorage.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AI.Internal;

/// <summary>
/// LLM-based blob classifier that also participates in the <see cref="IBlobValidator"/> pipeline,
/// built on the <see cref="IStructuredCompletion"/> primitive (ADR-064).
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
/// <para>
/// Classification is fail-soft: any unavailable / unusable AI response yields
/// <see cref="UnknownClassification"/>. The quota guard is applied by the primitive.
/// </para>
/// </remarks>
internal sealed partial class AIBlobClassifierService(
    IStructuredCompletion structuredCompletion,
    IOptions<BlobStorageAIOptions> options,
    ILogger<AIBlobClassifierService> logger) : IAIBlobClassifier, IBlobValidator
{
    private static readonly BlobClassification UnknownClassification = new(
        Category: "unknown",
        Confidence: 0.0,
        DetectedTags: [],
        ContainsPiiInFileName: false);

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

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = ClassificationInstruction,
            Content = fileName,
            ContentLabel = "Filename",
            Context = [new("Content type", contentType)],
            WorkspaceName = config.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<ClassificationJson> result = await structuredCompletion
                .CompleteAsync<ClassificationJson>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.Status != StructuredCompletionStatus.Succeeded)
            {
                LogClassificationUnavailable(logger, fileName, result.Status.ToString());
                return UnknownClassification;
            }

            ClassificationJson parsed = result.Value!;
            return new BlobClassification(
                Category: parsed.Category ?? "unknown",
                Confidence: Math.Clamp(parsed.Confidence, 0.0, 1.0),
                DetectedTags: parsed.Tags ?? [],
                ContainsPiiInFileName: parsed.ContainsPiiInFileName);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogClassificationTimeout(logger, fileName, config.TimeoutSeconds);
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

    internal const string ClassificationInstruction =
        """
        Classify the file described by the supplied metadata. Choose a category from:
        invoice, identity_document, photo, contract, report, spreadsheet, presentation, archive, code, other.
        Provide a confidence between 0.0 and 1.0 and any descriptive tags. For containsPiiInFileName,
        report true when the filename contains patterns resembling social security numbers, email
        addresses, phone numbers, national ID numbers, or full personal names.
        """;

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI blob classification unavailable ({Status}) for file '{FileName}' — treated as unknown")]
    private static partial void LogClassificationUnavailable(ILogger logger, string fileName, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI blob classification timed out after {TimeoutSeconds}s for file '{FileName}'")]
    private static partial void LogClassificationTimeout(ILogger logger, string fileName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PII detected in filename '{FileName}' — upload rejected")]
    private static partial void LogPiiDetected(ILogger logger, string fileName);

    /// <summary>Internal DTO for deserializing the LLM JSON response.</summary>
    internal sealed record ClassificationJson(
        string? Category,
        double Confidence,
        List<string>? Tags,
        bool ContainsPiiInFileName);
}
