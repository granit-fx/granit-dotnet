namespace Granit.Privacy.DataDeletion;

/// <summary>
/// State of a deferred personal data deletion request (GDPR Art. 17 cooling-off period).
/// </summary>
public enum DeletionRequestState
{
    /// <summary>Grace period active — deletion not yet executed, user may cancel.</summary>
    Deferred,

    /// <summary>
    /// Deadline reached and every registered provider has acknowledged erasure of its data.
    /// This is the only state that proves GDPR Art. 17 completion — the fan-in over provider
    /// acknowledgements guarantees it is never reached while a downstream deletion is still pending.
    /// </summary>
    Executed,

    /// <summary>User cancelled the deletion request during the grace period.</summary>
    Cancelled,

    /// <summary>
    /// Deadline reached and the provider fan-out has started, but not every provider has
    /// acknowledged yet. Intermediate state between <see cref="Deferred"/> and
    /// <see cref="Executed"/>: the request stays here until the last acknowledgement arrives
    /// (<see cref="Events.PersonalDataDeletedEto"/> from each provider) or the acknowledgement
    /// window elapses.
    /// </summary>
    Executing,

    /// <summary>
    /// The acknowledgement window elapsed before every provider confirmed erasure. At least one
    /// provider never acknowledged (permanent failure, dead-letter, or an unregistered handler).
    /// The request is NOT provably complete — an operator must reconcile the missing providers
    /// (surfaced by the stuck-deletion metric/log) before the erasure can be attested for Art. 17.
    /// </summary>
    PartiallyExecuted,
}
