using System.Diagnostics;

namespace Granit.Notifications.Twilio.Diagnostics;

/// <summary>OpenTelemetry activity source for the Twilio SMS/WhatsApp provider.</summary>
internal static class NotificationsTwilioActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.Twilio";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendSms = "twilio.send-sms";
        public const string SendWhatsApp = "twilio.send-whatsapp";
    }
}
