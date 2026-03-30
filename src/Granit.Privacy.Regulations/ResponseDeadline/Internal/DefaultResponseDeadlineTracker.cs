namespace Granit.Privacy.Regulations.ResponseDeadline.Internal;

/// <summary>
/// Default deadline tracker using calendar days from the regulation profile.
/// All Tier 1 and Tier 2 regulations use calendar days for response deadlines.
/// </summary>
internal sealed class DefaultResponseDeadlineTracker : IResponseDeadlineTracker
{
    public Task<DateTimeOffset> CalculateDeadlineAsync(
        PrivacyRequestType requestType,
        PrivacyRegulationProfile profile,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default)
    {
        int? days = requestType switch
        {
            PrivacyRequestType.SubjectAccessRequest => profile.SubjectAccessRequestDays,
            PrivacyRequestType.DeletionRequest => profile.DeletionRequestDays ?? profile.SubjectAccessRequestDays,
            PrivacyRequestType.RectificationRequest => profile.RectificationRequestDays ?? profile.SubjectAccessRequestDays,
            PrivacyRequestType.DataPortability => profile.SubjectAccessRequestDays,
            PrivacyRequestType.ProcessingRestriction => profile.SubjectAccessRequestDays,
            PrivacyRequestType.OptOut => profile.DeletionRequestDays ?? profile.SubjectAccessRequestDays,
            _ => profile.SubjectAccessRequestDays,
        };

        DateTimeOffset deadline = requestedAt.AddDays(days ?? 30);
        return Task.FromResult(deadline);
    }
}
