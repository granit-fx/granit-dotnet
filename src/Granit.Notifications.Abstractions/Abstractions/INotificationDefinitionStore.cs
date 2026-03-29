namespace Granit.Notifications.Abstractions;

/// <summary>
/// Read-only registry of notification definitions populated at startup.
/// </summary>
public interface INotificationDefinitionStore
{
    /// <summary>Returns all registered definitions.</summary>
    IReadOnlyList<NotificationDefinition> GetAll();

    /// <summary>Returns the definition for the given type name, or <c>null</c>.</summary>
#pragma warning disable CA1716 // Identifier does not conflict with a language keyword in this context
    NotificationDefinition? Get(string notificationTypeName);
#pragma warning restore CA1716
}
