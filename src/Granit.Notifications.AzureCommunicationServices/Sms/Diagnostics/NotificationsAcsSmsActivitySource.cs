using System.Diagnostics;

namespace Granit.Notifications.AzureCommunicationServices.Sms.Diagnostics;

/// <summary>OpenTelemetry activity source for the ACS SMS provider.</summary>
internal static class NotificationsAcsSmsActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.AzureCommunicationServices.Sms";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendSms = "acs-sms.send";
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string Recipient = "acs-sms.recipient";
    }
}
