using System.Diagnostics;

namespace Granit.Notifications.Scaleway.Diagnostics;

/// <summary>OpenTelemetry activity source for the Scaleway email provider.</summary>
internal static class NotificationsScalewayActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.Scaleway";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string Send = "scaleway-email.send";
    }
}
