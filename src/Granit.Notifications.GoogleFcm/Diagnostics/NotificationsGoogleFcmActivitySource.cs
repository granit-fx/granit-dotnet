using System.Diagnostics;

namespace Granit.Notifications.GoogleFcm.Diagnostics;

/// <summary>OpenTelemetry activity source for the FCM mobile push provider.</summary>
internal static class NotificationsGoogleFcmActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.GoogleFcm";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendPush = "fcm.send-push";
    }
}
