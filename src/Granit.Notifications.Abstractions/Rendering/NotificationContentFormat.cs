namespace Granit.Notifications.Rendering;

/// <summary>Target shape a notification channel needs from the content renderer.</summary>
public enum NotificationContentFormat
{
    /// <summary>
    /// Full HTML document: localized template, layout wrap, subject extracted from
    /// <c>&lt;title&gt;</c>. Used by the email channel.
    /// </summary>
    Html,

    /// <summary>Short plain text (SMS, mobile push body). Template support lands with text templates (#2963 follow-up).</summary>
    PlainText,

    /// <summary>Title + short body pair (mobile push, in-app). Template support lands with text templates (#2963 follow-up).</summary>
    TitleBody,

    /// <summary>Markdown body (chat channels such as Zulip). Template support lands with text templates (#2963 follow-up).</summary>
    Markdown,
}
