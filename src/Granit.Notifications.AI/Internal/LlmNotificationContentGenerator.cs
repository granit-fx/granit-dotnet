using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Options;
using Granit.Notifications.AI.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Notifications.AI.Internal;

/// <summary>
/// LLM-based notification content generator that produces a localized subject and body via the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064). Returns <c>null</c> on any non-success
/// outcome so callers fall back to template-based content.
/// </summary>
internal sealed partial class LlmNotificationContentGenerator(
    IStructuredCompletion structuredCompletion,
    IOptions<NotificationsAIOptions> options,
    ILogger<LlmNotificationContentGenerator> logger) : IAINotificationContentGenerator
{
    /// <inheritdoc/>
    public async Task<NotificationContent?> GenerateAsync(
        NotificationDeliveryContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        NotificationsAIOptions config = options.Value;

        string culture = context.Culture ?? "en";

        // GDPR gate: the Data payload may carry personal data. It only reaches the model
        // when the host explicitly opted in — otherwise the LLM works from the notification
        // type, severity and culture alone.
        string dataJson = config.AllowPersonalDataInPrompts && context.Data.ValueKind != JsonValueKind.Undefined
            ? context.Data.GetRawText()
            : "{}";

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var request = new StructuredCompletionRequest
        {
            Instruction = BuildInstruction(culture),
            Content = dataJson,
            ContentLabel = "Context data",
            Context =
            [
                new("Notification type", context.NotificationTypeName),
                new("Severity", context.Severity.ToString()),
            ],
            WorkspaceName = config.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<NotificationContentResponse> result = await structuredCompletion
                .CompleteAsync<NotificationContentResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            if (result.Status != StructuredCompletionStatus.Succeeded)
            {
                LogContentGenerationRejected(logger, context.NotificationTypeName, result.Status.ToString());
                return null;
            }

            NotificationContentResponse content = result.Value!;

            if (string.IsNullOrWhiteSpace(content.Subject) || string.IsNullOrWhiteSpace(content.Body))
            {
                return null;
            }

            return new NotificationContent(content.Subject, content.Body);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogContentGenerationTimeout(logger, context.NotificationTypeName, config.TimeoutSeconds);
            return null;
        }
    }

    private static string BuildInstruction(string culture) =>
        $"""
        Generate a notification subject and body, in locale '{culture}', for the notification described
        by the context type/severity and the data block.
        The subject should be concise (under 100 characters). The body should be informative but brief.
        Do NOT include HTML, scripts, or any markup in the generated text.
        """;

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI notification content generation rejected (status: {Status}) for type '{NotificationTypeName}'")]
    private static partial void LogContentGenerationRejected(ILogger logger, string notificationTypeName, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "AI notification content generation timed out after {TimeoutSeconds}s for type '{NotificationTypeName}'")]
    private static partial void LogContentGenerationTimeout(ILogger logger, string notificationTypeName, int timeoutSeconds);
}
