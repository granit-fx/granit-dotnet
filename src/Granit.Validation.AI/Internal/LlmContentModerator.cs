using System.Text.Json;
using Granit.AI;
using Granit.AI.Internal;
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

            // CreateAsync builds a fresh client per call (no cache) — dispose
            // deterministically so the HttpMessageHandler doesn't linger until GC.
            using IChatClient chatClient = await chatClientFactory
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
        catch (JsonException ex)
        {
            // Adversarial/malformed LLM output — fail-closed to prevent bypass
            LogModerationParseFailure(ex);
            return new ModerationResult { IsAcceptable = false, Flags = [] };
        }
        catch (Exception ex)
        {
            LogModerationFailed(ex);
            return AcceptWithWarning();
        }
    }

    internal static string BuildPrompt(string text, string? context)
    {
        var pb = new PromptBuilder(maxInputLength: 50_000);

        pb.AppendInstruction("""
            Analyze the following text for content policy violations.
            Return ONLY a JSON object with this exact structure (no markdown, no explanation):
            { "isAcceptable": true/false, "flags": [{ "category": "Toxic|Harassment|PromptInjection|Spam|Violence|SelfHarm|Sexual|Other", "description": "brief description", "severity": 0.0-1.0 }] }
            """);

        if (context is not null)
        {
            pb.AppendUserData("Context", context);
        }

        pb.AppendUserTextBlock("Text to analyze", text);

        return pb.Build();
    }

    internal static ModerationResult ParseResponse(string responseText, double severityThreshold)
    {
        string json = LlmResponseHelper.StripMarkdownCodeFences(responseText);

        LlmModerationResponse? parsed = JsonSerializer.Deserialize<LlmModerationResponse>(json, JsonOptions);

        if (parsed is null)
        {
            // Fail-closed: unparseable LLM response suggests adversarial manipulation
            return new ModerationResult { IsAcceptable = false, Flags = [] };
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI content moderation returned unparseable response — content rejected (fail-closed)")]
    private partial void LogModerationParseFailure(Exception exception);

    private sealed record LlmModerationResponse(bool IsAcceptable, List<LlmModerationFlag>? Flags);

    private sealed record LlmModerationFlag(string? Category, string? Description, double Severity);
}
