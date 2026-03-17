using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
using Granit.Privacy.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.AI.Internal;

/// <summary>
/// LLM-based implementation of <see cref="IAIPiiDetector"/> that sends text to an AI model
/// with a structured prompt for PII detection.
/// </summary>
internal sealed partial class LlmPiiDetector(
    IAIChatClientFactory chatClientFactory,
    IOptions<PrivacyAIOptions> options,
    ILogger<LlmPiiDetector> logger) : IAIPiiDetector
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly PiiDetectionResult EmptyResult = new()
    {
        ContainsPii = false,
        Items = [],
    };

    /// <inheritdoc />
    public async Task<PiiDetectionResult> ScanAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        PrivacyAIOptions privacyOptions = options.Value;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(privacyOptions.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(privacyOptions.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(text);

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, prompt),
            };

            ChatResponse response = await chatClient
                .GetResponseAsync(messages, cancellationToken: linkedCts.Token)
                .ConfigureAwait(false);

            string responseText = LlmResponseHelper.StripMarkdownCodeFences(response.Text ?? string.Empty);

            LlmPiiResponse? llmResult = JsonSerializer.Deserialize<LlmPiiResponse>(responseText, SerializerOptions);

            if (llmResult is null)
            {
                LogDeserializationNull();
                return EmptyResult;
            }

            List<DetectedPii> items = [];

            if (llmResult.Items is { Count: > 0 })
            {
                foreach (LlmPiiItem item in llmResult.Items)
                {
                    if (Enum.TryParse<PiiType>(item.Type, ignoreCase: true, out PiiType piiType))
                    {
                        items.Add(new DetectedPii(piiType, item.Description ?? string.Empty));
                    }
                    else
                    {
                        items.Add(new DetectedPii(PiiType.Other, item.Description ?? string.Empty));
                    }
                }
            }

            var result = new PiiDetectionResult
            {
                ContainsPii = llmResult.ContainsPii,
                Items = items,
            };

            LogScanCompleted(result.ContainsPii, result.Items.Count);
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogScanTimeout(privacyOptions.TimeoutSeconds);
            return EmptyResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogScanFailed(ex.Message);
            return EmptyResult;
        }
    }

    private static string BuildPrompt(string text) =>
        $$"""
          Scan the following text for personally identifiable information (PII).
          Return ONLY valid JSON matching this schema:
          { "containsPii": bool, "items": [{ "type": "Email", "description": "Found email in sentence 2" }] }

          Valid type values: PersonName, Email, PhoneNumber, Address, NationalId, DateOfBirth, BankAccount, CreditCard, Other.

          If no PII is found, return: { "containsPii": false, "items": [] }

          Text to scan:
          ---
          {{text}}
          ---

          Return ONLY valid JSON, no markdown, no explanation.
          """;

    [LoggerMessage(Level = LogLevel.Information, Message = "PII scan completed: containsPii={ContainsPii}, itemCount={ItemCount}")]
    private partial void LogScanCompleted(bool containsPii, int itemCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PII scan failed, returning no-PII result: {ErrorMessage}")]
    private partial void LogScanFailed(string errorMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PII scan timed out after {TimeoutSeconds}s, returning no-PII result")]
    private partial void LogScanTimeout(int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "PII scan LLM response deserialization returned null, returning no-PII result")]
    private partial void LogDeserializationNull();

    /// <summary>
    /// Internal DTO for deserializing LLM JSON response.
    /// </summary>
    private sealed record LlmPiiResponse(bool ContainsPii, List<LlmPiiItem>? Items);

    /// <summary>
    /// Internal DTO for a single PII item from the LLM response.
    /// </summary>
    private sealed record LlmPiiItem(string? Type, string? Description);
}
