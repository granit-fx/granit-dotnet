using System.Diagnostics;

namespace Granit.Notifications.Brevo.Diagnostics;

/// <summary>OpenTelemetry activity source for the Brevo multi-channel provider.</summary>
internal static class NotificationsBrevoActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.Brevo";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendEmail = "brevo.send-email";
        public const string SendSms = "brevo.send-sms";
        public const string SendWhatsApp = "brevo.send-whatsapp";
    }
}
