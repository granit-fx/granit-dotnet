namespace Granit.Notifications.Email;

/// <summary>Email message to send via an <see cref="IEmailSender"/> provider.</summary>
public sealed record EmailMessage
{
    /// <summary>Recipient email address.</summary>
    public required string To { get; init; }

    /// <summary>Email subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>HTML body.</summary>
    public required string HtmlBody { get; init; }

    /// <summary>Optional plain text fallback body.</summary>
    public string? PlainTextBody { get; init; }

    /// <summary>Optional recipient display name (e.g. "John Doe").</summary>
    public string? ToName { get; init; }

    /// <summary>Optional sender email address override (takes precedence over provider defaults).</summary>
    public string? FromEmailOverride { get; init; }

    /// <summary>Optional sender display name override (takes precedence over provider defaults).</summary>
    public string? FromNameOverride { get; init; }

    /// <summary>
    /// Custom email headers (e.g. <c>List-Unsubscribe</c>, <c>List-Unsubscribe-Post</c>).
    /// <c>null</c> means no custom headers are added.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}
