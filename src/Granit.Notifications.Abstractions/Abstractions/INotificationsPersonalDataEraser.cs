namespace Granit.Notifications.Abstractions;

/// <summary>
/// Bulk erasure of a data subject's notification data (inbox, preferences, subscriptions)
/// for GDPR Art. 17. Kept separate from <see cref="IUserNotificationWriter"/>,
/// <see cref="INotificationPreferenceWriter"/> and <see cref="INotificationSubscriptionWriter"/>
/// — those model normal per-item write operations, not a cross-entity subject-scoped purge.
/// </summary>
public interface INotificationsPersonalDataEraser
{
    /// <summary>
    /// Permanently deletes every inbox item, preference and subscription owned by the
    /// given user within a tenant scope. A no-op for entities the user has none of.
    /// </summary>
    /// <returns>
    /// The total number of rows physically deleted across all entity sets — carried into
    /// <c>PersonalDataDeletedEto.AffectedRecords</c> as the ISO 27001 deletion audit evidence.
    /// </returns>
    Task<int> EraseUserDataAsync(
        string userId,
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
