using System.Diagnostics;

namespace Granit.Notifications.Zulip.Diagnostics;

/// <summary>OpenTelemetry activity source for the Zulip chat provider.</summary>
internal static class NotificationsZulipActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.Zulip";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string SendMessage = "zulip.send-message";
    }
}
