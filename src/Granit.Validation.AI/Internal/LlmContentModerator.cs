using Granit.AI;
using Granit.Validation.AI.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Validation.AI.Internal;

/// <summary>
/// LLM-backed content moderator that analyzes text for policy violations via the
/// <see cref="IStructuredCompletion"/> primitive (ADR-064).
/// </summary>
/// <remarks>
/// Mixed failure policy: a transport failure or timeout is <b>fail-open</b> (content
/// accepted, warning logged) so moderation never blocks the request, while an
/// unparseable / refused response is <b>fail-closed</b> (content rejected) to deny an
/// adversarial bypass. The aggressive moderation timeout (kept low on purpose) is applied
/// by the caller-side token; the primitive owns schema enforcement, usage tracking, and
/// PII-safe error mapping.
/// </remarks>
internal sealed partial class LlmContentModerator(
    IStructuredCompletion structuredCompletion,
    IOptions<ValidationAIOptions> options,
    ILogger<LlmContentModerator> logger) : IAIContentModerator
{
    /// <inheritdoc/>
    public async Task<ModerationResult> ModerateAsync(
        string text,
        string? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        ValidationAIOptions config = options.Value;

        // Aggressive moderation timeout (must not block the request). Layered over the
        // primitive's own timeout; whichever fires first cancels the call. When ours fires,
        // the primitive rethrows the cancellation and we fail open below.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(config.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        var request = new StructuredCompletionRequest
        {
            Instruction = ModerationInstruction,
            Content = text,
            ContentLabel = "Text to analyze",
            Context = context is null ? null : [new("Context", context)],
            WorkspaceName = config.WorkspaceName,
        };

        try
        {
            StructuredCompletionResult<LlmModerationResponse> result = await structuredCompletion
                .CompleteAsync<LlmModerationResponse>(request, linkedCts.Token)
                .ConfigureAwait(false);

            switch (result.Status)
            {
                case StructuredCompletionStatus.Succeeded:
                    return BuildResult(result.Value!, config.SeverityThreshold);

                case StructuredCompletionStatus.TransportFailure:
                    // Provider/transport problem — fail open so validation never blocks.
                    LogModerationFailed();
                    return AcceptWithWarning();

                default:
                    // Refused or unparseable output suggests adversarial manipulation — fail closed.
                    LogModerationParseFailure();
                    return new ModerationResult { IsAcceptable = false, Flags = [] };
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            LogModerationTimeout(config.TimeoutSeconds);
            return AcceptWithWarning();
        }
    }

    internal const string ModerationInstruction =
        """
        Analyze the supplied text for content policy violations and return a structured result.
        For each violation add a flag whose category is one of:
        Toxic, Harassment, PromptInjection, Spam, Violence, SelfHarm, Sexual, Other.
        Set isAcceptable to false when any violation is present, with a brief description and a
        severity between 0.0 and 1.0 per flag.
        """;

    private static ModerationResult BuildResult(LlmModerationResponse parsed, double severityThreshold)
    {
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
    private partial void LogModerationFailed();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "AI content moderation returned an unusable response — content rejected (fail-closed)")]
    private partial void LogModerationParseFailure();

    internal sealed record LlmModerationResponse(bool IsAcceptable, List<LlmModerationFlag>? Flags);

    internal sealed record LlmModerationFlag(string? Category, string? Description, double Severity);
}
