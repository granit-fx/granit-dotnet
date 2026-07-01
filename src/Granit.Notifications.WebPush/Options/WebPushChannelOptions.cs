namespace Granit.Notifications.WebPush.Options;

/// <summary>Web Push VAPID configuration options.</summary>
public sealed class WebPushChannelOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:WebPush";

    /// <summary>VAPID subject (mailto: or https: URL).</summary>
    public string VapidSubject { get; set; } = string.Empty;

    /// <summary>VAPID public key (Base64 URL-safe).</summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>VAPID private key (Base64 URL-safe, should come from Vault).</summary>
    public string VapidPrivateKey { get; set; } = string.Empty;
}
