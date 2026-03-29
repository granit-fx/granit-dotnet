namespace Granit.Notifications.Abstractions;

/// <summary>
/// Context for registering notification definitions at startup.
/// </summary>
public interface INotificationDefinitionContext
{
    /// <summary>Registers a notification definition.</summary>
    void Add(NotificationDefinition definition);
}
