using System.Text.Json.Serialization;

namespace Granit.Notifications.Endpoints.Dtos;

/// <summary>
/// Request to register a browser Web Push subscription. Mirrors the W3C
/// <c>PushSubscriptionJSON</c> shape emitted by <c>PushSubscription.toJSON()</c>.
/// </summary>
public sealed record WebPushSubscriptionRegisterRequest
{
    /// <summary>Push service endpoint URL for this subscription.</summary>
    public required string Endpoint { get; init; }

    /// <summary>Subscription expiration time (Unix epoch ms), or null when it never expires.</summary>
    public long? ExpirationTime { get; init; }

    /// <summary>Encryption keys emitted by the browser.</summary>
    public required WebPushSubscriptionKeys Keys { get; init; }
}

/// <summary>Encryption keys of a browser Web Push subscription (W3C <c>keys</c> member).</summary>
public sealed record WebPushSubscriptionKeys
{
    // Both names are sealed with an explicit [JsonPropertyName]: the W3C Push API casing is
    // strict and must not drift if a consumer changes the global JSON serialization policy.

    /// <summary>P-256 DH public key (Base64 URL-safe).</summary>
    [JsonPropertyName("p256dh")]
    public required string P256dh { get; init; }

    /// <summary>Authentication secret (Base64 URL-safe).</summary>
    [JsonPropertyName("auth")]
    public required string Auth { get; init; }
}
