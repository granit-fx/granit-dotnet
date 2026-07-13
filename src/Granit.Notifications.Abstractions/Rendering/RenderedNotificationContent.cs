namespace Granit.Notifications.Rendering;

/// <summary>
/// Localized content produced by <see cref="INotificationContentRenderer"/> for one
/// notification delivery.
/// </summary>
/// <param name="Title">
/// Title / subject line, when the template provides one (email <c>&lt;title&gt;</c>,
/// push title). <see langword="null"/> when the template has none.
/// </param>
/// <param name="Body">
/// Rendered body in the requested <see cref="NotificationContentFormat"/> — a full HTML
/// document for <see cref="NotificationContentFormat.Html"/>, otherwise text/markdown.
/// </param>
public sealed record RenderedNotificationContent(string? Title, string Body);
