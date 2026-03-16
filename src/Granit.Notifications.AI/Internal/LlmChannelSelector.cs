using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AI.Internal;

/// <summary>
/// LLM-based channel selector that recommends optimal delivery channels based on context analysis.
/// </summary>
internal sealed partial class LlmChannelSelector(
    IAIChatClientFactory chatClientFactory,
    IOptions<NotificationsAIOptions> options,
    ILogger<LlmChannelSelector> logger) : IAIChannelSelector
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SelectChannelsAsync(
        NotificationDeliveryContext context,
        IReadOnlyList<string> availableChannels,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(availableChannels);

        if (availableChannels.Count == 0)
        {
            return [];
        }

        NotificationsAIOptions config = options.Value;

        try
        {
            IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, cancellationToken)
                .ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, timeoutCts.Token);

            string prompt = BuildChannelSelectionPrompt(context, availableChannels);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            IReadOnlyList<string> selected = ParseChannelSelectionResponse(responseText, availableChannels);

            return selected.Count > 0 ? selected : availableChannels;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogChannelSelectionTimeout(logger, context.NotificationTypeName, config.TimeoutSeconds);
            return availableChannels;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogChannelSelectionFailure(logger, context.NotificationTypeName, ex);
            return availableChannels;
        }
    }

    internal static string BuildChannelSelectionPrompt(
        NotificationDeliveryContext context,
        IReadOnlyList<string> availableChannels)
    {
        string channelsJson = JsonSerializer.Serialize(availableChannels);

        return $$"""
            Given notification severity '{{context.Severity}}', time '{{context.OccurredAt:O}}', and available channels {{channelsJson}}, recommend the optimal channels for delivery.
            Notification type: '{{context.NotificationTypeName}}'.
            Return JSON only, no markdown fences: an array of channel names, e.g. ["email", "push"].
            Only include channels from the available list. Order by priority (most important first).
            For critical/fatal severity, prefer all real-time channels. For info, prefer less intrusive channels.
            """;
    }

    internal static IReadOnlyList<string> ParseChannelSelectionResponse(
        string responseText,
        IReadOnlyList<string> availableChannels)
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
            List<string>? parsed = JsonSerializer.Deserialize<List<string>>(trimmed, JsonOptions);

            if (parsed is null || parsed.Count == 0)
            {
                return [];
            }

            // Only keep channels that are actually available (case-insensitive)
            HashSet<string> availableSet = new(availableChannels, StringComparer.OrdinalIgnoreCase);

            return parsed
                .Where(c => availableSet.Contains(c))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI channel selection timed out after {TimeoutSeconds}s for type '{NotificationTypeName}'")]
    private static partial void LogChannelSelectionTimeout(ILogger logger, string notificationTypeName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI channel selection failed for type '{NotificationTypeName}'")]
    private static partial void LogChannelSelectionFailure(ILogger logger, string notificationTypeName, Exception exception);
}
