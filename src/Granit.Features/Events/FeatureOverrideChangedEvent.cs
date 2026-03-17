namespace Granit.Features.Events;

/// <summary>
/// Raised when a feature override is created, updated, or deleted, with old and new values
/// for ISO 27001 A.12.4 configuration change audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Complements <see cref="FeatureValueChangedEvent"/> (which triggers cache invalidation only).
/// This event carries the full before/after snapshot needed for audit logging.
/// </para>
/// <para>
/// Published by <c>EfCoreFeatureStore</c> after every write operation.
/// </para>
/// </remarks>
/// <param name="FeatureName">The feature whose override changed.</param>
/// <param name="TenantId">The tenant scope, or <c>null</c> for global.</param>
/// <param name="OldValue">Previous value (<c>null</c> if the override was created).</param>
/// <param name="NewValue">New value (<c>null</c> if the override was deleted).</param>
/// <param name="Timestamp">UTC timestamp of the change.</param>
public sealed record FeatureOverrideChangedEvent(
    string FeatureName,
    Guid? TenantId,
    string? OldValue,
    string? NewValue,
    DateTimeOffset Timestamp);
