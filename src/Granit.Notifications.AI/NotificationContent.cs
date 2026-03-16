namespace Granit.Notifications.AI;

/// <summary>
/// AI-generated notification content containing a subject line and body text.
/// </summary>
/// <param name="Subject">Short subject line for the notification.</param>
/// <param name="Body">Full body text of the notification.</param>
public sealed record NotificationContent(string Subject, string Body);
