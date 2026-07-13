using System.Diagnostics;

namespace Granit.Notifications.Smtp.Diagnostics;

/// <summary>OpenTelemetry activity source for the SMTP email provider.</summary>
internal static class NotificationsSmtpActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.Smtp";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendEmail = "smtp.send-email";
    }
}
