using Granit.Domain;

namespace Granit.Notifications;

/// <summary>
/// Marker interface for entities whose property changes should automatically
/// generate notifications to followers.
/// </summary>
public interface ITrackedEntity
{
    /// <summary>Entity type name for the notification system (e.g. "Patient").</summary>
    static abstract string EntityTypeName { get; }

    /// <summary>Entity identifier as string for <see cref="EntityReference"/>.</summary>
    string GetEntityId();

    /// <summary>
    /// Properties to monitor. When a tracked property changes, a notification is published
    /// to all entity followers with the associated <see cref="TrackedPropertyConfig"/>.
    /// </summary>
    static abstract IReadOnlyDictionary<string, TrackedPropertyConfig> TrackedProperties { get; }
}
