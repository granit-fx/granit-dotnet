using System.Reflection;
using System.Text;
using System.Text.Json;
using Granit.AI.Extraction.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.AI.Extraction.Internal;

/// <summary>
/// Default implementation of <see cref="IDocumentExtractor{TResult}"/> that uses an LLM
/// via <see cref="IAIChatClientFactory"/> to extract structured data from document text.
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
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(extractionOptions.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string schemaDescription = BuildSchemaDescription();
            string prompt = BuildPrompt(schemaDescription, content);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt),
            };

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            // Strip markdown code fences if present
            responseText = StripMarkdownCodeFences(responseText);

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
            LogDeserializationFailed(typeof(TResult).Name, ex.Message);
            return ExtractionResult.Failed<TResult>($"Failed to deserialize LLM response: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogExtractionFailed(typeof(TResult).Name, ex.Message);
            return ExtractionResult.Failed<TResult>($"Extraction failed: {ex.Message}");
        }
    }

    private static string BuildSchemaDescription()
    {
        var sb = new StringBuilder();
        PropertyInfo[] properties = typeof(TResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            string typeName = GetFriendlyTypeName(property.PropertyType);
            sb.AppendLine($"- \"{property.Name}\": {typeName}");
        }

        return sb.ToString();
    }

    private static string GetFriendlyTypeName(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        string name = underlying switch
        {
            _ when underlying == typeof(string) => "string",
            _ when underlying == typeof(int) => "integer",
            _ when underlying == typeof(long) => "integer",
            _ when underlying == typeof(decimal) => "number",
            _ when underlying == typeof(double) => "number",
            _ when underlying == typeof(float) => "number",
            _ when underlying == typeof(bool) => "boolean",
            _ when underlying == typeof(DateTime) => "date-time string (ISO 8601)",
            _ when underlying == typeof(DateOnly) => "date string (yyyy-MM-dd)",
            _ when underlying == typeof(Guid) => "UUID string",
            _ => underlying.Name,
        };

        return Nullable.GetUnderlyingType(type) is not null ? $"{name} (nullable)" : name;
    }

    private static string BuildPrompt(string schemaDescription, string content) =>
        $"""
         Extract structured data from the following document.
         Return a JSON object matching this schema:
         {schemaDescription}
         Document:
         ---
         {content}
         ---

         Return ONLY valid JSON, no markdown, no explanation.
         """;

    private static string StripMarkdownCodeFences(string text)
    {
        ReadOnlySpan<char> span = text.AsSpan().Trim();

        if (span.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            span = span["```json".Length..];
        }
        else if (span.StartsWith("```", StringComparison.Ordinal))
        {
            span = span["```".Length..];
        }

        if (span.EndsWith("```", StringComparison.Ordinal))
        {
            span = span[..^"```".Length];
        }

        return span.Trim().ToString();
    }

    private static double EstimateConfidence(ChatResponse response)
    {
        // Check for provider-supplied confidence in additional properties
        if (response.AdditionalProperties?.TryGetValue("confidence", out object? confidenceValue) is true
            && confidenceValue is double confidence)
        {
            return confidence;
        }

        // Check finish reason as a heuristic
        if (response.FinishReason == ChatFinishReason.Stop)
        {
            return 0.85;
        }

        // Default moderate confidence when no signal is available
        return 0.75;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Extraction succeeded for {TypeName} with confidence {Confidence:F2}")]
    private partial void LogExtractionSucceeded(string typeName, double confidence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Low confidence extraction for {TypeName}: {Confidence:F2} < threshold {Threshold:F2}")]
    private partial void LogLowConfidence(string typeName, double confidence, double threshold);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction failed for {TypeName}: {ErrorMessage}")]
    private partial void LogExtractionFailed(string typeName, string errorMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "Extraction timed out for {TypeName} after {TimeoutSeconds}s")]
    private partial void LogExtractionTimeout(string typeName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Deserialization returned null for {TypeName}")]
    private partial void LogDeserializationNull(string typeName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize LLM response for {TypeName}: {ErrorMessage}")]
    private partial void LogDeserializationFailed(string typeName, string errorMessage);
}
