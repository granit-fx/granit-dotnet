using System.Diagnostics;

namespace Granit.Notifications.AzureNotificationHubs.Diagnostics;

/// <summary>
/// OpenTelemetry activity source for the Azure Notification Hubs push provider.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class NotificationsAzureNotificationHubsActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.AzureNotificationHubs";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        /// <summary>Send push notification via Azure Notification Hubs.</summary>
        public const string Send = "anh.send";
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        /// <summary>Number of target devices.</summary>
        public const string DeviceCount = "anh.device_count";
    }
}
