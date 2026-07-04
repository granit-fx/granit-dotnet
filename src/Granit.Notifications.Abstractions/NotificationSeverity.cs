namespace Granit.Notifications;

/// <summary>Severity level of a notification.</summary>
public enum NotificationSeverity
{
    /// <summary>Informational notification.</summary>
    Info,
    /// <summary>Success notification.</summary>
    Success,
    /// <summary>Warning notification.</summary>
    Warning,
    /// <summary>Error notification.</summary>
    Error,
    /// <summary>Fatal/critical notification.</summary>
    Fatal,
}
