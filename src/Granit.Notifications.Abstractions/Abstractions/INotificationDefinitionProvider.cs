namespace Granit.Notifications.Abstractions;

/// <summary>
/// Contract for declaring notification types in code.
/// Implement and register with <c>services.AddNotificationDefinitions&lt;T&gt;()</c>.
/// </summary>
public interface INotificationDefinitionProvider
{
    /// <summary>Declares notification definitions using the provided context.</summary>
    void Define(INotificationDefinitionContext context);
}
