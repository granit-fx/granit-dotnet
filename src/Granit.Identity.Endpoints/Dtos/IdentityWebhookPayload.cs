namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Generic webhook payload from an identity provider.
/// The application host translates provider-specific formats into this structure.
/// </summary>
/// <param name="EventType">Event type: <c>user_created</c>, <c>user_updated</c>, <c>user_deleted</c>, or <c>login</c>.</param>
/// <param name="UserId">External user ID in the identity provider.</param>
/// <param name="Timestamp">Event timestamp from the provider.</param>
/// <param name="SessionId">For a <c>login</c> event: the IdP session id (Keycloak <c>sid</c>) the new session is keyed by — must match what the session provider surfaces so anomaly detection can locate it. Ignored for user_* events.</param>
/// <param name="IpAddress">For a <c>login</c> event: the client IP, used off the critical path for geolocation. Server-side only; never logged or echoed.</param>
/// <param name="UserAgent">For a <c>login</c> event: the client User-Agent, when the provider includes it.</param>
public sealed record IdentityWebhookPayload(
    string EventType,
    string UserId,
    DateTimeOffset? Timestamp = null,
    string? SessionId = null,
    string? IpAddress = null,
    string? UserAgent = null);
