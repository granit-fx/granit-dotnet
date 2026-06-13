namespace Granit.Identity.Endpoints.Options;

/// <summary>
/// Configuration for the "was this you?" session-review flow. Bind from <c>"Identity:SessionReview"</c>.
/// </summary>
public sealed class SessionReviewOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Identity:SessionReview";

    /// <summary>
    /// How long a review link stays valid. A security alert may be opened a while after it arrives, so the
    /// default is generous (7 days); the token is single-use at the action layer regardless. Default: 7 days.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(7);
}
