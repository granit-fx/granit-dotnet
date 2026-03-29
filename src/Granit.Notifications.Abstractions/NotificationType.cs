namespace Granit.Notifications;

/// <summary>
/// Strongly-typed notification type declaration. Each notification type in the application
/// should be a singleton instance of a class deriving from this.
/// </summary>
/// <typeparam name="TData">The notification data payload type.</typeparam>
public abstract class NotificationType<TData> where TData : notnull
{
    /// <summary>The CLR type of the notification data payload.</summary>
    public Type DataType => typeof(TData);

    /// <summary>Unique name (convention: <c>"Module.NotificationName"</c>).</summary>
    public abstract string Name { get; }

    /// <summary>Default severity level.</summary>
    public virtual NotificationSeverity DefaultSeverity => NotificationSeverity.Info;

    /// <summary>Default channels for this notification type.</summary>
    public abstract IReadOnlyList<string> DefaultChannels { get; }
}
