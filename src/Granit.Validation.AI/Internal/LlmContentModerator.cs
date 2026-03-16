using System.Text.Json;
using Granit.AI;
using Granit.Validation.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Validation.AI.Internal;

/// <summary>
/// LLM-backed content moderator that analyzes text for policy violations.
/// </summary>
/// <remarks>
/// Uses a fail-open design: when the LLM is unavailable or times out,
/// content is accepted and a warning is logged for manual review.
/// </remarks>
internal sealed partial class LlmContentModerator(
    IAIChatClientFactory chatClientFactory,
    IOptions<ValidationAIOptions> options,
    ILogger<LlmContentModerator> logger) : IAIContentModerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<ModerationResult> ModerateAsync(
        string text,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        ValidationAIOptions config = options.Value;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);

            IChatClient chatClient = await chatClientFactory
                .CreateAsync(config.WorkspaceName, linkedCts.Token)
                .ConfigureAwait(false);

            string prompt = BuildPrompt(text, context);

            ChatResponse response = await chatClient.GetResponseAsync(
                prompt, cancellationToken: linkedCts.Token).ConfigureAwait(false);

            string responseText = response.Text ?? string.Empty;

            return ParseResponse(responseText, config.SeverityThreshold);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogModerationTimeout(config.TimeoutSeconds);
            return AcceptWithWarning();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogModerationFailed(ex);
            return AcceptWithWarning();
        }
    }

    internal static string BuildPrompt(string text, string? context)
    {
        string contextPart = context is not null
            ? $" The text appears in the following context: {context}."
            : string.Empty;

        return $$"""
            Analyze the following text for content policy violations.{{contextPart}}
            Return ONLY a JSON object with this exact structure (no markdown, no explanation):
            { "isAcceptable": true/false, "flags": [{ "category": "Toxic|Harassment|PromptInjection|Spam|Violence|SelfHarm|Sexual|Other", "description": "brief description", "severity": 0.0-1.0 }] }

            Text to analyze:
            """
            + text;
    }

    internal static ModerationResult ParseResponse(string responseText, double severityThreshold)
    {
        // Strip markdown code fences if present.
        string json = responseText.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            int firstNewline = json.IndexOf('\n');
            int lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                json = json[(firstNewline + 1)..lastFence].Trim();
            }
        }

        LlmModerationResponse? parsed = JsonSerializer.Deserialize<LlmModerationResponse>(json, JsonOptions);

        if (parsed is null)
        {
            return new ModerationResult { IsAcceptable = true, Flags = [] };
        }

        List<ModerationFlag> filteredFlags = [];

        if (parsed.Flags is not null)
        {
            foreach (LlmModerationFlag flag in parsed.Flags)
            {
                if (flag.Severity >= severityThreshold &&
                    Enum.TryParse(flag.Category, ignoreCase: true, out ModerationCategory category))
                {
                    filteredFlags.Add(new ModerationFlag(category, flag.Description ?? string.Empty, flag.Severity));
                }
            }
        }

        return new ModerationResult
        {
            IsAcceptable = parsed.IsAcceptable,
            Flags = filteredFlags,
        };
    }

    private static ModerationResult AcceptWithWarning() =>
        new() { IsAcceptable = true, Flags = [] };

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI content moderation timed out after {TimeoutSeconds}s — content accepted (fail-open), flagged for manual review")]
    private partial void LogModerationTimeout(int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI content moderation failed — content accepted (fail-open), flagged for manual review")]
    private partial void LogModerationFailed(Exception exception);

    private sealed record LlmModerationResponse(bool IsAcceptable, List<LlmModerationFlag>? Flags);

    private sealed record LlmModerationFlag(string? Category, string? Description, double Severity);
}
