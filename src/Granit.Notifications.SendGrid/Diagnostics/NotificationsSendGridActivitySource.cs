using System.Diagnostics;

namespace Granit.Notifications.SendGrid.Diagnostics;

/// <summary>OpenTelemetry activity source for the SendGrid email provider.</summary>
internal static class NotificationsSendGridActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.SendGrid";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string Send = "sendgrid.send";
    }
}
