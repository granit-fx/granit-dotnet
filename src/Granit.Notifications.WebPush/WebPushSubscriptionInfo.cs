namespace Granit.Notifications.WebPush;

/// <summary>W3C Push API subscription received from the browser.</summary>
public sealed record WebPushSubscriptionInfo
{
    /// <summary>Push service endpoint URL.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Subscription expiration time (Unix epoch ms), or null if no expiration.</summary>
    public long? ExpirationTime { get; init; }

    /// <summary>P-256 DH public key (Base64 URL-safe).</summary>
    public required string P256dh { get; init; }

    /// <summary>Authentication secret (Base64 URL-safe).</summary>
    public required string Auth { get; init; }
}
