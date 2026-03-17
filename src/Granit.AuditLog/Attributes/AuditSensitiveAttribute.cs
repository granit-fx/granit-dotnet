namespace Granit.AuditLog.Attributes;

/// <summary>
/// Marks a property as sensitive. Its value will be recorded as <c>"***"</c>
/// in <see cref="Domain.AuditPropertyChange"/> entries instead of the actual value.
/// </summary>
/// <remarks>
/// Use for PII, secrets, or any data that should not appear in the audit trail
/// (GDPR Art. 5 — data minimization).
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditSensitiveAttribute : Attribute;
