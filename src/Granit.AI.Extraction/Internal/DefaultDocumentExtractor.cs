using Granit.AI.Extraction.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Extraction.Internal;

/// <summary>
/// Default <see cref="IDocumentExtractor{TResult}"/>: a thin "document + confidence" surface
/// over <see cref="IStructuredCompletion"/> (ADR-064). It delegates the LLM call, schema
/// enforcement, fallback, usage tracking, and PII-safe error handling to the primitive, then
/// adds the document-extraction concerns — a confidence estimate and the review-threshold
/// workflow — and maps <see cref="StructuredCompletionStatus"/> onto <see cref="ExtractionStatus"/>.
/// </summary>
internal sealed partial class DefaultDocumentExtractor<TResult>(
    IStructuredCompletion structuredCompletion,
    IOptions<ExtractionOptions> options,
    ILogger<DefaultDocumentExtractor<TResult>> logger) : IDocumentExtractor<TResult>
    where TResult : class
{
    /// <inheritdoc />
    public async Task<ExtractionResult<TResult>> ExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ExtractionOptions extractionOptions = options.Value;

        var completionRequest = new StructuredCompletionRequest
        {
            Instruction = request.Instruction,
            Content = request.Content,
            ContentLabel = request.ContentLabel,
            Context = request.Context,
            WorkspaceName = extractionOptions.WorkspaceName,
        };

        StructuredCompletionResult<TResult> result = await structuredCompletion
            .CompleteAsync<TResult>(completionRequest, cancellationToken)
            .ConfigureAwait(false);

        switch (result.Status)
        {
            case StructuredCompletionStatus.Succeeded:
                TResult data = result.Value!;
                double confidence = EstimateConfidence(result);

                if (confidence < extractionOptions.ReviewThreshold)
                {
                    LogLowConfidence(typeof(TResult).Name, confidence, extractionOptions.ReviewThreshold);
                    return ExtractionResult.NeedsReview(
                        data,
                        confidence,
                        [$"Confidence score ({confidence:F2}) is below the review threshold ({extractionOptions.ReviewThreshold:F2})."],
                        result.ModelId);
                }

                LogExtractionSucceeded(typeof(TResult).Name, confidence);
                return ExtractionResult.Success(data, confidence, modelId: result.ModelId);

            case StructuredCompletionStatus.ModelRefused:
                LogExtractionFailed(typeof(TResult).Name, result.Status.ToString());
                return ExtractionResult.Failed<TResult>("The model returned no usable content.", result.ModelId);

            case StructuredCompletionStatus.SchemaViolation:
                LogExtractionFailed(typeof(TResult).Name, result.Status.ToString());
                return ExtractionResult.Failed<TResult>("Failed to deserialize the LLM response.", result.ModelId);

            default:
                LogExtractionFailed(typeof(TResult).Name, result.Status.ToString());
                return ExtractionResult.Failed<TResult>(
                    result.ErrorMessage ?? "Extraction failed due to a transport or provider error.",
                    result.ModelId);
        }
    }

    private static double EstimateConfidence(StructuredCompletionResult<TResult> result)
    {
        if (result.Metadata?.TryGetValue("confidence", out object? confidenceValue) is true
            && confidenceValue is double confidence)
        {
            return confidence;
        }

        return result.FinishReason == ChatFinishReason.Stop ? 0.85 : 0.75;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Extraction succeeded for {TypeName} with confidence {Confidence:F2}")]
    private partial void LogExtractionSucceeded(string typeName, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Low confidence extraction for {TypeName}: {Confidence:F2} < threshold {Threshold:F2}")]
    private partial void LogLowConfidence(string typeName, double confidence, double threshold);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction failed for {TypeName} (status: {Status})")]
    private partial void LogExtractionFailed(string typeName, string status);
}
