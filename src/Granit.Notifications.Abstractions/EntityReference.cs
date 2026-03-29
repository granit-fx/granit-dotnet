namespace Granit.Notifications;

/// <summary>
/// Polymorphic reference to a business entity, used to link notifications to their source entity.
/// </summary>
public sealed record EntityReference(string EntityType, string EntityId);
