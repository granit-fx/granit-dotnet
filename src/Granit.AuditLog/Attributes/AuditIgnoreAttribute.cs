namespace Granit.AuditLog.Attributes;

/// <summary>
/// Excludes an entity class or property from audit log change tracking.
/// </summary>
/// <remarks>
/// Apply to an entity class to skip it entirely, or to individual properties
/// to exclude them from <see cref="Domain.AuditPropertyChange"/> capture.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute;
