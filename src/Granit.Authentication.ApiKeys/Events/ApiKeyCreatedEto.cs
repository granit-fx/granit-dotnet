using Granit.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Published when a new API key is created. Consumed by handlers for audit
/// and notification purposes.
/// </summary>
/// <param name="ApiKeyId">The unique identifier of the new key.</param>
/// <param name="Name">Display name of the key.</param>
/// <param name="Type">The key type.</param>
public sealed record ApiKeyCreatedEto(Guid ApiKeyId, string Name, ApiKeyType Type) : IIntegrationEvent;
