namespace Granit.Privacy.Regulations.ResponseDeadline;

/// <summary>
/// Calculates and tracks regulation-mandated response deadlines for privacy requests.
/// The default implementation uses calendar days from <see cref="PrivacyRegulationProfile"/>.
/// Applications needing business day calculation can provide a custom implementation.
/// </summary>
public interface IResponseDeadlineTracker
{
    /// <summary>
    /// Calculates the response deadline for a given request type and regulation profile.
    /// Returns the deadline as an absolute <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="requestType">The type of privacy request.</param>
    /// <param name="profile">The applicable regulation profile.</param>
    /// <param name="requestedAt">When the request was submitted (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DateTimeOffset> CalculateDeadlineAsync(
        PrivacyRequestType requestType,
        PrivacyRegulationProfile profile,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);
}
