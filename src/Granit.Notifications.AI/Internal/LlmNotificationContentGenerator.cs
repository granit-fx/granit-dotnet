using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AI.Internal;

/// <summary>
/// LLM-based notification content generator that produces localized subject and body text.
/// </summary>
internal sealed partial class LlmNotificationContentGenerator(
    IAIChatClientFactory chatClientFactory,
    IOptions<NotificationsAIOptions> options,
    ILogger<LlmNotificationContentGenerator> logger) : IAINotificationContentGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<NotificationContent?> GenerateAsync(
        NotificationDeliveryContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        NotificationsAIOptions config = options.Value;

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, cancellationToken)
                .ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            string prompt = BuildContentPrompt(context);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            return ParseContentResponse(responseText);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogContentGenerationTimeout(logger, context.NotificationTypeName, config.TimeoutSeconds);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogContentGenerationFailure(logger, context.NotificationTypeName, ex);
            return null;
        }
    }

    internal static string BuildContentPrompt(NotificationDeliveryContext context)
    {
        string culture = context.Culture ?? "en";
        string dataJson = context.Data.ValueKind != JsonValueKind.Undefined
            ? context.Data.GetRawText()
            : "{}";

        return $$"""
            Generate a notification subject and body for notification type '{{context.NotificationTypeName}}' in locale '{{culture}}'.
            Context data: {{dataJson}}
            Severity: {{context.Severity}}
            Return JSON only, no markdown fences: {"subject": "<string>", "body": "<string>"}
            The subject should be concise (under 100 characters). The body should be informative but brief.
            """;
    }

    internal static NotificationContent? ParseContentResponse(string responseText)
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
            ContentJson? parsed = JsonSerializer.Deserialize<ContentJson>(trimmed, JsonOptions);

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Subject) || string.IsNullOrWhiteSpace(parsed.Body))
            {
                return null;
            }

            return new NotificationContent(parsed.Subject, parsed.Body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI notification content generation timed out after {TimeoutSeconds}s for type '{NotificationTypeName}'")]
    private static partial void LogContentGenerationTimeout(ILogger logger, string notificationTypeName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI notification content generation failed for type '{NotificationTypeName}'")]
    private static partial void LogContentGenerationFailure(ILogger logger, string notificationTypeName, Exception exception);

    /// <summary>
    /// Internal DTO for deserializing the LLM JSON response.
    /// </summary>
    private sealed record ContentJson(string? Subject, string? Body);
}
