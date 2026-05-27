using System.Text.Json;
using Granit.AI.Extraction.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Extraction.Internal;

/// <summary>
/// Default implementation of <see cref="IDocumentExtractor{TResult}"/> that uses an LLM
/// via <see cref="IAIChatClientFactory"/> to extract structured data from document text.
/// Uses <see cref="ChatResponseFormat.ForJsonSchema{T}"/> to delegate schema generation
/// and structured output enforcement to the MEAI pipeline.
/// </summary>
internal sealed partial class DefaultDocumentExtractor<TResult>(
    IAIChatClientFactory chatClientFactory,
    IOptions<ExtractionOptions> options,
    ILogger<DefaultDocumentExtractor<TResult>> logger) : IDocumentExtractor<TResult>
    where TResult : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly ChatOptions StructuredOutputOptions = new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema<TResult>(),
    };

    /// <inheritdoc />
    public async Task<ExtractionResult<TResult>> ExtractAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        ExtractionOptions extractionOptions = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(extractionOptions.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await chatClientFactory
                .CreateAsync(extractionOptions.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, BuildPrompt(content)),
            };

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, StructuredOutputOptions, linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            TResult? data = JsonSerializer.Deserialize<TResult>(responseText, SerializerOptions);

            if (data is null)
            {
                LogDeserializationNull(typeof(TResult).Name);
                return ExtractionResult.Failed<TResult>("Deserialization returned null.");
            }

            double confidence = EstimateConfidence(response);
            var warnings = new List<string>();

            if (confidence < extractionOptions.ReviewThreshold)
            {
                warnings.Add($"Confidence score ({confidence:F2}) is below the review threshold ({extractionOptions.ReviewThreshold:F2}).");
                LogLowConfidence(typeof(TResult).Name, confidence, extractionOptions.ReviewThreshold);
                return ExtractionResult.NeedsReview(data, confidence, warnings);
            }

            LogExtractionSucceeded(typeof(TResult).Name, confidence);
            return ExtractionResult.Success(data, confidence);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogExtractionTimeout(typeof(TResult).Name, extractionOptions.TimeoutSeconds);
            return ExtractionResult.Failed<TResult>(
                $"Extraction timed out after {extractionOptions.TimeoutSeconds} seconds.");
        }
        catch (JsonException ex)
        {
            // Never log or surface ex.Message: a JSON parse error embeds a fragment of
            // the (LLM-produced, possibly PII-bearing) response. Type only.
            LogDeserializationFailed(typeof(TResult).Name, ex.GetType().Name);
            return ExtractionResult.Failed<TResult>("Failed to deserialize the LLM response.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never log or surface ex.Message: some IChatClient providers echo the prompt
            // payload in transport exception messages (4xx content-policy / schema reject).
            LogExtractionFailed(typeof(TResult).Name, ex.GetType().Name);
            return ExtractionResult.Failed<TResult>("Extraction failed due to a transport or provider error.");
        }
    }

    private static string BuildPrompt(string content) =>
        $"""
         Extract structured data from the following document.
         Document:
         ---
         {content}
         ---
         """;

    private static double EstimateConfidence(ChatResponse response)
    {
        if (response.AdditionalProperties?.TryGetValue("confidence", out object? confidenceValue) is true
            && confidenceValue is double confidence)
        {
            return confidence;
        }

        return response.FinishReason == ChatFinishReason.Stop ? 0.85 : 0.75;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Extraction succeeded for {TypeName} with confidence {Confidence:F2}")]
    private partial void LogExtractionSucceeded(string typeName, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Low confidence extraction for {TypeName}: {Confidence:F2} < threshold {Threshold:F2}")]
    private partial void LogLowConfidence(string typeName, double confidence, double threshold);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction failed for {TypeName} (exception type: {ExceptionType})")]
    private partial void LogExtractionFailed(string typeName, string exceptionType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction timed out for {TypeName} after {TimeoutSeconds}s")]
    private partial void LogExtractionTimeout(string typeName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Deserialization returned null for {TypeName}")]
    private partial void LogDeserializationNull(string typeName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize LLM response for {TypeName} (exception type: {ExceptionType})")]
    private partial void LogDeserializationFailed(string typeName, string exceptionType);
}
