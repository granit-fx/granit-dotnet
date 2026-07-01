namespace Granit.Notifications.Endpoints.Dtos;

/// <summary>Request to unregister a browser Web Push subscription by its endpoint.</summary>
public sealed record WebPushSubscriptionRemoveRequest
{
    /// <summary>Push service endpoint URL of the subscription to remove.</summary>
    public required string Endpoint { get; init; }
}
