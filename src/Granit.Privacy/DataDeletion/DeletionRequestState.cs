namespace Granit.Privacy.DataDeletion;

/// <summary>
/// State of a deferred personal data deletion request (GDPR Art. 17 cooling-off period).
/// </summary>
public enum DeletionRequestState
{
    /// <summary>Grace period active — deletion not yet executed, user may cancel.</summary>
    Deferred = 0,

    /// <summary>Deadline reached — <see cref="Events.PersonalDataDeletionRequestedEto"/> published, providers are deleting.</summary>
    Executed = 1,

    /// <summary>User cancelled the deletion request during the grace period.</summary>
    Cancelled = 2,
}
