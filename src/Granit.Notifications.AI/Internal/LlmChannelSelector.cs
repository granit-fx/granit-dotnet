using System.Globalization;
using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Options;
using Granit.Notifications.AI.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AI.Internal;

/// <summary>
/// LLM-based channel selector that recommends optimal delivery channels via the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064). Falls back to the full available
/// channel list whenever the model is unavailable or returns nothing usable.
/// </summary>
internal sealed partial class LlmChannelSelector(
    IStructuredCompletion structuredCompletion,
    IOptions<NotificationsAIOptions> options,
    ILogger<LlmChannelSelector> logger) : IAIChannelSelector
{
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

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = BuildInstruction(availableChannels),
            Content = BuildContent(context),
            ContentLabel = "Notification",
            WorkspaceName = config.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<ChannelSelectionResponse> result = await structuredCompletion
                .CompleteAsync<ChannelSelectionResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.Status != StructuredCompletionStatus.Succeeded)
            {
                LogChannelSelectionRejected(logger, context.NotificationTypeName, result.Status.ToString());
                return availableChannels;
            }

            List<string> selected = FilterToAvailable(result.Value!.Channels, availableChannels);

            // Default-to-all: an empty (or fully-rejected) recommendation must not silently drop delivery.
            return selected.Count > 0 ? selected : availableChannels;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogChannelSelectionTimeout(logger, context.NotificationTypeName, config.TimeoutSeconds);
            return availableChannels;
        }
    }

    private static List<string> FilterToAvailable(List<string> selected, IReadOnlyList<string> availableChannels)
    {
        HashSet<string> availableSet = new(availableChannels, StringComparer.OrdinalIgnoreCase);
        return selected.Where(availableSet.Contains).ToList();
    }

    private static string BuildInstruction(IReadOnlyList<string> availableChannels)
    {
        string channelsJson = JsonSerializer.Serialize(availableChannels);

        return $"""
            Recommend the optimal delivery channels for the notification described in the data block,
            choosing only from this available list: {channelsJson}.
            Order the result by priority (most important first) and include only channels from that list.
            For critical/fatal severity, prefer all real-time channels. For info, prefer less intrusive channels.
            """;
    }

    private static string BuildContent(NotificationDeliveryContext context) =>
        string.Create(CultureInfo.InvariantCulture, $"""
            Notification type: {context.NotificationTypeName}
            Severity: {context.Severity}
            Time: {context.OccurredAt:O}
            """);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI channel selection rejected (status: {Status}) for type '{NotificationTypeName}', falling back to all channels")]
    private static partial void LogChannelSelectionRejected(ILogger logger, string notificationTypeName, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI channel selection timed out after {TimeoutSeconds}s for type '{NotificationTypeName}', falling back to all channels")]
    private static partial void LogChannelSelectionTimeout(ILogger logger, string notificationTypeName, int timeoutSeconds);
}
