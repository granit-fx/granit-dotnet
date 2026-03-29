namespace Granit.Notifications;

/// <summary>Severity level of a notification.</summary>
public enum NotificationSeverity
{
    /// <summary>Informational notification.</summary>
    Info = 0,
    /// <summary>Success notification.</summary>
    Success = 1,
    /// <summary>Warning notification.</summary>
    Warning = 2,
    /// <summary>Error notification.</summary>
    Error = 3,
    /// <summary>Fatal/critical notification.</summary>
    Fatal = 4,
}
