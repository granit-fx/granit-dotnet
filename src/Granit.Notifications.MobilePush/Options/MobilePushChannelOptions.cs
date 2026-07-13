namespace Granit.Notifications.MobilePush.Options;

/// <summary>Configuration for the mobile push notification channel.</summary>
public sealed class MobilePushChannelOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:MobilePush";

    /// <summary>
    /// Provider key for Keyed Services resolution. Must match a registered
    /// <c>IMobilePushSender</c> key: "GoogleFcm", "AwsSns", or "AzureNotificationHubs".
    /// </summary>
    public string Provider { get; set; } = "GoogleFcm";
}
