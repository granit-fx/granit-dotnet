using System.Diagnostics;

namespace Granit.Notifications.AwsSns.MobilePush.Diagnostics;

/// <summary>OpenTelemetry activity source for the AWS SNS mobile push provider.</summary>
internal static class NotificationsAwsSnsMobilePushActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Notifications.AwsSns.MobilePush";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string Send = "sns-push.send";
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string DeviceCount = "sns-push.device_count";
    }
}
