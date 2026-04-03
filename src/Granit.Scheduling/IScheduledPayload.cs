namespace Granit.Scheduling;

/// <summary>
/// Marker interface for scheduled action payloads.
/// </summary>
/// <remarks>
/// Payloads are serialized to JSON and stored in the <c>ScheduledAction</c> entity.
/// At the scheduled time, the payload is deserialized and delivered to its
/// Wolverine handler as a standard message.
/// <para>
/// Implementations should be <c>sealed record</c> types with only serializable properties.
/// Do <strong>not</strong> include EF Core entities, streams, or non-serializable types.
/// </para>
/// <example>
/// <code>
/// public sealed record ApplyPlanChangePayload(
///     Guid TenantId,
///     Guid SubscriptionId,
///     Guid NewPlanId) : IScheduledPayload;
/// </code>
/// </example>
/// </remarks>
public interface IScheduledPayload;
