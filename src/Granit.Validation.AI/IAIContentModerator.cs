namespace Granit.Validation.AI;

/// <summary>
/// Analyzes text for content policy violations using AI-powered moderation.
/// </summary>
/// <remarks>
/// <para>
/// This is a reusable service that validators can call within their rules.
/// It is NOT a validator itself — validators are per-type and use this service
/// to moderate user-provided text fields.
/// </para>
/// <para>
/// The implementation uses a fail-open design: when the LLM is unavailable or
/// times out, content is accepted and a warning is logged for manual review.
/// </para>
/// </remarks>
public interface IAIContentModerator
{
    /// <summary>
    /// Analyzes the given text for content policy violations.
    /// </summary>
    /// <param name="text">The text content to moderate.</param>
    /// <param name="context">
    /// Optional context about where the text appears (e.g. "user profile bio", "comment on article").
    /// Helps the LLM make more accurate moderation decisions.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ModerationResult"/> indicating whether the content is acceptable
    /// and listing any policy flags above the configured severity threshold.
    /// </returns>
    Task<ModerationResult> ModerateAsync(string text, string? context = null, CancellationToken cancellationToken = default);
}
