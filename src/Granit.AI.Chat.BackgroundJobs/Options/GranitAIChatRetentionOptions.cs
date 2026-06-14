using System.ComponentModel.DataAnnotations;

namespace Granit.AI.Chat.BackgroundJobs.Options;

/// <summary>
/// Retention policy for chat conversations (ADR-067, GDPR data minimisation), bound from the
/// <c>AI:Chat:Retention</c> configuration section. Retention is <strong>opt-in</strong>: with the
/// default <see cref="RetentionDays"/> of 0 the cleanup job purges nothing.
/// </summary>
public sealed class GranitAIChatRetentionOptions
{
    /// <summary>The configuration section bound to these options.</summary>
    public const string SectionName = "AI:Chat:Retention";

    /// <summary>
    /// Conversations whose last activity is older than this many days are purged. 0 (default)
    /// disables retention purging entirely.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int RetentionDays { get; set; }

    /// <summary>Number of conversations deleted per batch. Default 500.</summary>
    [Range(1, int.MaxValue)]
    public int CleanupBatchSize { get; set; } = 500;
}
